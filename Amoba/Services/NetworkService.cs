using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace Amoba.Services
{
    public class NetworkService : INetworkService, IDisposable
    {
        public event EventHandler<GameFoundEventArgs> GameFound;
        public event EventHandler<GameStartedEventArgs> GameStarted;
        public event EventHandler<int> MoveReceived;
        public event EventHandler OpponentDisconnected;
        public event EventHandler<string> NetworkErrorOccurred;
        public event EventHandler HostConnectionEstablished;
        public event EventHandler RematchReceived;
        public event EventHandler OpponentLeft;
        public event EventHandler LeaveAcknowledged;
        public event EventHandler<string> ChatMessageReceived;
        public event EventHandler OpponentIsTyping;

        // --- Konstansok ---
        private const string MulticastGroupAddress = "239.255.42.99";
        private const string UdpPort = "9090";
        private const string TcpGamePort = "9091";
        private const string BroadcastMessagePrefix = "AMOBA_GAME_HOST;";
        private const int KeepaliveIntervalSeconds = 10;
        private const int KeepaliveTimeoutSeconds = 30;

        // --- Hálózati Objektumok ---
        private DatagramSocket _hostSocket;
        private DatagramSocket _listenerSocket;
        private StreamSocketListener _tcpListener;
        private StreamSocket _gameSocket;

        private DataReader _socketReader;
        private DataWriter _socketWriter;

        public string CachedOpponentName { get; private set; }

        private HostName _multicastHostName;
        private Timer _broadcastTimer;
        private Timer _keepaliveTimer;
        private CancellationTokenSource _readCts;

        // =======================================================
        // --- "Lakat" az aszinkron írói műveletekhez ---
        // =======================================================
        private readonly SemaphoreSlim _writerSemaphore = new SemaphoreSlim(1, 1);


        // --- Konstruktor ---
        public NetworkService()
        {
            try { _multicastHostName = new HostName(MulticastGroupAddress); }
            catch (ArgumentException ex) { Debug.WriteLine($"Érvénytelen multicast cím: {ex.Message}"); }
        }

        // ===================================================================
        // UDP HOSTOLÁS
        // ===================================================================
        public async Task StartHostingAsync(string playerName)
        {
            if (_multicastHostName == null || _broadcastTimer != null || _hostSocket != null)
            {
                await Task.CompletedTask; return;
            }
            try
            {
                _hostSocket = new DatagramSocket();
                await _hostSocket.BindServiceNameAsync(UdpPort);
                _hostSocket.JoinMulticastGroup(_multicastHostName);
                _broadcastTimer = new Timer(async (state) => await SendBroadcastMessage(playerName), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
                Debug.WriteLine($"Hostolás elindítva, küldés a {MulticastGroupAddress}:{UdpPort} címre.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"KRITIKUS HIBA AZ UDP INDÍTÁSAKOR: {ex.Message}");
                StopHosting();
                throw;
            }
        }

        private async Task SendBroadcastMessage(string playerName)
        {
            if (_hostSocket == null || _multicastHostName == null) return;
            try
            {
                string localIp = GetLocalIpAddress() ?? "UnknownIP";
                string message = $"{BroadcastMessagePrefix}{playerName};{localIp}";
                using (var stream = await _hostSocket.GetOutputStreamAsync(_multicastHostName, UdpPort))
                using (var writer = new DataWriter(stream))
                {
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    writer.WriteBytes(data);
                    await writer.StoreAsync();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Hiba az üzenetküldéskor: {ex.Message}"); }
        }

        public void StopHosting()
        {
            _broadcastTimer?.Dispose(); _broadcastTimer = null;
            _hostSocket?.Dispose(); _hostSocket = null;
            Debug.WriteLine("Hostolás (UDP) leállítva.");
        }

        // ===================================================================
        // UDP KERESÉS
        // ===================================================================
        public async Task StartDiscoveringAsync()
        {
            if (_listenerSocket != null || _multicastHostName == null) { return; }
            try
            {
                _listenerSocket = new DatagramSocket();
                _listenerSocket.MessageReceived += ListenerSocket_MessageReceived;
                await _listenerSocket.BindServiceNameAsync(UdpPort);
                _listenerSocket.JoinMulticastGroup(_multicastHostName);
                Debug.WriteLine($"Keresés elindítva, figyelés a(z) {MulticastGroupAddress}:{UdpPort} címen...");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba a keresés indításakor: {ex.Message}");
                StopDiscovering();
                NetworkErrorOccurred?.Invoke(this, $"Hiba a keresés indításakor: {ex.Message}.");
            }
        }

        public void StopDiscovering()
        {
            if (_listenerSocket != null)
            {
                _listenerSocket.MessageReceived -= ListenerSocket_MessageReceived;
                _listenerSocket.Dispose(); _listenerSocket = null;
                Debug.WriteLine("Keresés leállítva.");
            }
        }

        private void ListenerSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (DataReader reader = args.GetDataReader())
                {
                    uint unreadBytes = reader.UnconsumedBufferLength;
                    if (unreadBytes == 0) return;
                    string message = reader.ReadString(unreadBytes).Trim('\0').Trim();
                    if (message.StartsWith(BroadcastMessagePrefix))
                    {
                        string[] parts = message.Split(';');
                        if (parts.Length >= 3)
                        {
                            string playerName = parts[1];
                            string ipAddress = args.RemoteAddress.CanonicalName;
                            string myIp = GetLocalIpAddress();
                            if (myIp != null && ipAddress == myIp) return;
                            GameFound?.Invoke(this, new GameFoundEventArgs(playerName, ipAddress));
                            Debug.WriteLine($"Játék találat: {playerName} ({ipAddress})");
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Hiba az üzenet fogadásakor: {ex.Message}"); }
        }

        // ===================================================================
        // TCP KAPCSOLAT FOGADÁSA (HOST)
        // ===================================================================
        public async Task StartAcceptingConnectionsAsync()
        {
            Debug.WriteLine("TCP LISTENER INDÍTÁSA...");

            if (_tcpListener != null) return;
            try
            {
                _tcpListener = new StreamSocketListener();
                _tcpListener.ConnectionReceived += TcpListener_ConnectionReceived;
                await _tcpListener.BindServiceNameAsync(TcpGamePort);
                Debug.WriteLine($"TCP Listener aktív a porton: {TcpGamePort}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba a TCP listener indításakor: {ex.Message}");
                _tcpListener?.Dispose(); _tcpListener = null;
                throw;
            }
        }

        private void TcpListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            StopHosting();

            if (_gameSocket != null) { args.Socket.Dispose(); return; }

            _gameSocket = args.Socket;
            Debug.WriteLine($"Kapcsolat elfogadva: {_gameSocket.Information.RemoteHostName}");

            _tcpListener?.Dispose();
            _tcpListener = null;

            _socketReader = new DataReader(_gameSocket.InputStream);
            _socketReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            _socketWriter = new DataWriter(_gameSocket.OutputStream);

            StartReading();
            StartKeepaliveTimer(); // Életjel indítása
            HostConnectionEstablished?.Invoke(this, EventArgs.Empty);
        }

        // ===================================================================
        // TCP KAPCSOLÓDÁS (JOINER)
        // ===================================================================
        public async Task ConnectToGameAsync(string hostIpAddress, string myPlayerName)
        {
            try
            {
                StopDiscovering();
                HostName remoteHost = new HostName(hostIpAddress);
                _gameSocket = new StreamSocket();
                await _gameSocket.ConnectAsync(remoteHost, TcpGamePort);
                Debug.WriteLine("Kapcsolódás sikeres! Olvasó/író létrehozása...");

                _socketReader = new DataReader(_gameSocket.InputStream);
                _socketReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                _socketWriter = new DataWriter(_gameSocket.OutputStream);

                StartReading();
                StartKeepaliveTimer(); // Életjel indítása

                await SendMessageAsync($"NAME;{myPlayerName}");
                Debug.WriteLine($"[CLIENT SEND] Név elküldve: {myPlayerName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba a kapcsolódáskor: {ex.Message}");
                NetworkErrorOccurred?.Invoke(this, $"Hiba a kapcsolódáskor: {ex.Message}");
                Disconnect();
            }
        }

        // ===================================================================
        // KOMMUNIKÁCIÓ ÉS LÉPÉSEK
        // ===================================================================
        private void StartKeepaliveTimer()
        {
            _keepaliveTimer?.Dispose();
            _keepaliveTimer = new Timer(
                async (state) => await SendKeepalivePingAsync(),
                null,
                TimeSpan.FromSeconds(KeepaliveIntervalSeconds),
                TimeSpan.FromSeconds(KeepaliveIntervalSeconds)
            );
        }

        private async void StartReading()
        {
            _readCts = new CancellationTokenSource();
            try
            {
                while (!_readCts.IsCancellationRequested)
                {
                    using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(KeepaliveTimeoutSeconds)))
                    using (var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_readCts.Token, timeoutCts.Token))
                    {
                        uint size;
                        try
                        {
                            size = await _socketReader.LoadAsync(sizeof(uint)).AsTask(combinedCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            if (timeoutCts.IsCancellationRequested)
                            {
                                throw new Exception($"Keepalive időtúllépés ({KeepaliveTimeoutSeconds}s). A kapcsolat bontva.");
                            }
                            throw;
                        }

                        if (size == 0) throw new Exception("Kapcsolat lezárva (0 bájt olvasva).");

                        uint messageLength = _socketReader.ReadUInt32();

                        uint actualMessageSize;
                        try
                        {
                            using (var bodyTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(KeepaliveTimeoutSeconds)))
                            using (var bodyCombinedCts = CancellationTokenSource.CreateLinkedTokenSource(_readCts.Token, bodyTimeoutCts.Token))
                            {
                                actualMessageSize = await _socketReader.LoadAsync(messageLength).AsTask(bodyCombinedCts.Token);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw new Exception($"Időtúllépés az üzenettörzs olvasásakor ({KeepaliveTimeoutSeconds}s).");
                        }

                        if (actualMessageSize == 0 && messageLength > 0) throw new Exception("Kapcsolat lezárva (üzenet olvasása közben).");

                        if (actualMessageSize == messageLength)
                        {
                            string message = _socketReader.ReadString(actualMessageSize);
                            HandleReceivedMessage(message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_readCts.IsCancellationRequested)
                {
                    Debug.WriteLine("StartReading: Feladat szándékosan leállítva (Cancel).");
                }
                else
                {
                    Debug.WriteLine($"Olvasási hiba (Váratlan vagy Időtúllépés): {ex.Message}");
                    OpponentDisconnected?.Invoke(this, EventArgs.Empty);
                    Disconnect();
                }
            }
        }

        private void HandleReceivedMessage(string message)
        {

            if (message.StartsWith("START;"))
            {
                string[] parts = message.Split(';');
                if (parts.Length >= 3 && int.TryParse(parts[2], out int boardSize))
                {
                    string opponentName = parts[1];
                    GameStarted?.Invoke(this, new GameStartedEventArgs(opponentName, boardSize, false));
                }
            }
            else if (message.StartsWith("NAME;"))
            {
                string clientName = message.Substring(5);
                Debug.WriteLine($"[HOST RECV] Kliens név fogadva: {clientName}");
                this.CachedOpponentName = clientName;
            }
            else if (message.StartsWith("MOVE:"))
            {
                if (int.TryParse(message.Substring(5), out int moveIndex))
                {
                    MoveReceived?.Invoke(this, moveIndex);
                }
            }
            else if (message.StartsWith("REMATCH;"))
            {
                RematchReceived?.Invoke(this, EventArgs.Empty);
            }
            else if (message.StartsWith("LEAVE;"))
            {
                OpponentLeft?.Invoke(this, EventArgs.Empty);
            }
            else if (message.StartsWith("LEAVE_ACK;"))
            {
                LeaveAcknowledged?.Invoke(this, EventArgs.Empty);
            }
            else if (message.StartsWith("CHAT:"))
            {
                string chatText = message.Substring(5);
                ChatMessageReceived?.Invoke(this, chatText);
            }
            else if (message.StartsWith("PING;"))
            {
                // Csendben fogadjuk
            }
            else if (message.StartsWith("TYPING;"))
            {
                OpponentIsTyping?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task SendLeaveGameAsync()
        {
            await SendMessageAsync("LEAVE;");
            Debug.WriteLine("[NETWORK SEND] Leave üzenet küldve.");
        }

        public async Task SendRematchRequestAsync()
        {
            await SendMessageAsync("REMATCH;");
        }

        public async Task InitiateNetworkGameStartAsync(int boardSize, string myPlayerName)
        {
            if (_gameSocket == null)
            {
                Debug.WriteLine("Hiba: Játék indítása hívva, de nincs aktív TCP kapcsolat.");
                NetworkErrorOccurred?.Invoke(this, "Hiba: Nincs kapcsolat az ellenféllel.");
                return;
            }
            try
            {
                string message = $"START;{myPlayerName};{boardSize}";
                await SendMessageAsync(message);
                Debug.WriteLine($"[HOST SEND] Start üzenet küldve: {message}");

                string opponentName = CachedOpponentName ?? _gameSocket.Information.RemoteHostName.DisplayName;
                GameStarted?.Invoke(this, new GameStartedEventArgs(opponentName, boardSize, true)); // Host = true
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba a játék indításakor: {ex.Message}");
                NetworkErrorOccurred?.Invoke(this, $"Hiba a játék indításakor: {ex.Message}");
                Disconnect();
            }
        }

        public async Task SendMoveAsync(int index)
        {
            string moveMessage = $"MOVE:{index}";
            await SendMessageAsync(moveMessage);
        }

        private async Task SendKeepalivePingAsync()
        {
            try
            {
                await SendMessageAsync("PING;");
            }
            catch (Exception ex)
            {
                // Ha a PING küldése hibát dob (pl. mert a kapcsolat már megszakadt),
                // azt itt elkapjuk, és naplózzuk.
                // NEM dobjuk tovább, mert az összeomlasztaná a Timer szálat.
                Debug.WriteLine($"Keepalive hiba (elkapva): {ex.Message}");

                // A 'StartReading' metódus úgyis észlelni fogja a hibát
                // (időtúllépés vagy "0 bájt olvasva"), és az fogja
                // elindítani a 'OpponentDisconnected' eseményt.
            }
        }

        public async Task SendChatMessageAsync(string message)
        {
            await SendMessageAsync($"CHAT:{message}");
        }

        public async Task SendTypingIndicatorAsync()
        {
            await SendMessageAsync("TYPING;");
        }

        public async Task SendLeaveAckAsync()
        {
            try
            {
                await SendMessageAsync("LEAVE_ACK;");
                Debug.WriteLine("[NETWORK SEND] Leave ACK (nyugta) küldve.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba a 'Leave ACK' küldésekor: {ex.Message}");
            }
        }

        private async Task SendMessageAsync(string message)
        {
            // =======================================================
            // --- Szálbiztos zárolás SemaphoreSlim-mel ---
            // =======================================================

            // Várunk, amíg miénk lesz a "lakat". Ez nem blokkolja a UI szálat.
            await _writerSemaphore.WaitAsync();
            try
            {
                if (_socketWriter == null)
                {
                    Debug.WriteLine("SendMessageAsync: Socket Writer nincs inicializálva, küldés kihagyva.");
                    throw new InvalidOperationException("Socket Writer nincs inicializálva, küldés kihagyva.");
                }

                _socketWriter.WriteUInt32(_socketWriter.MeasureString(message));
                _socketWriter.WriteString(message);

                await _socketWriter.StoreAsync();
                //await _socketWriter.FlushAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba az üzenet küldésekor: {ex.Message}");
                throw new Exception("Hiba az üzenet küldésekor", ex);
            }
            finally
            {
                // Elengedjük a "lakatot", hogy más szál is írhasson.
                _writerSemaphore.Release();
            }
        }

        // ===================================================================
        // TAKARÍTÁS ÉS IP-Lekérdezés
        // ===================================================================
        public void Disconnect()
        {
            _readCts?.Cancel();
            _keepaliveTimer?.Dispose(); _keepaliveTimer = null;

            _socketReader?.Dispose();
            _socketReader = null;
            _socketWriter?.Dispose();
            _socketWriter = null;

            _tcpListener?.Dispose();
            _tcpListener = null;

            if (_gameSocket != null)
            {
                _gameSocket.Dispose();
                _gameSocket = null;
                Debug.WriteLine("TCP kapcsolat megszakadt.");
            }

            CachedOpponentName = null;
        }

        public void Dispose()
        {
            StopHosting();
            StopDiscovering();
            Disconnect();
            _writerSemaphore?.Dispose(); // A szemafor is IDisposable
            GC.SuppressFinalize(this);
        }

        private string GetLocalIpAddress()
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile?.NetworkAdapter == null) return null;
            var hostNames = NetworkInformation.GetHostNames();
            foreach (var hn in hostNames)
            {
                if (hn.IPInformation?.NetworkAdapter != null &&
                    hn.IPInformation.NetworkAdapter.NetworkAdapterId == profile.NetworkAdapter.NetworkAdapterId &&
                    hn.Type == HostNameType.Ipv4)
                {
                    return hn.CanonicalName;
                }
            }
            return null;
        }
    }
}