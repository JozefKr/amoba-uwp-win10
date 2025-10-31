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
using System.Runtime.InteropServices.WindowsRuntime;

namespace Amoba.Services
{
    // Ez a kód már a 'Host dönti el a méretet' logikát követi
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

        // --- Konstansok ---
        private const string MulticastGroupAddress = "239.255.42.99";
        private const string UdpPort = "9090";
        private const string TcpGamePort = "9091";
        private const string BroadcastMessagePrefix = "AMOBA_GAME_HOST;";

        // --- Hálózati Objektumok ---
        private DatagramSocket _hostSocket;
        private DatagramSocket _listenerSocket;
        private StreamSocketListener _tcpListener;
        private StreamSocket _gameSocket;

        // --- JAVÍTÁS: Osztályszintű Olvasó és Író ---
        private DataReader _socketReader;
        private DataWriter _socketWriter;
        // ---

        private HostName _multicastHostName;
        private Timer _broadcastTimer;
        private CancellationTokenSource _readCts;

        // --- Konstruktor ---
        public NetworkService()
        {
            try { _multicastHostName = new HostName(MulticastGroupAddress); }
            catch (ArgumentException ex) { Debug.WriteLine($"Érvénytelen multicast cím: {ex.Message}"); }
        }

        // ===================================================================
        // UDP HOSTOLÁS (Változatlan)
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
            // Mivel a Kliens sikeresen csatlakozott, már nincs szükségünk
            // az UDP hirdetésre. Állítsuk le.
            StopHosting();

            if (_gameSocket != null) { args.Socket.Dispose(); return; }

            _gameSocket = args.Socket;
            Debug.WriteLine($"Kapcsolat elfogadva: {_gameSocket.Information.RemoteHostName}");

            _tcpListener?.Dispose();
            _tcpListener = null;

            // --- Osztályszintű Olvasó/Író létrehozása ---
            _socketReader = new DataReader(_gameSocket.InputStream);
            _socketReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            _socketWriter = new DataWriter(_gameSocket.OutputStream);
            // ---

            StartReading(); // Figyelés indítása
            HostConnectionEstablished?.Invoke(this, EventArgs.Empty); // Jelzés a MainViewModel-nek
        }

        // ===================================================================
        // TCP KAPCSOLÓDÁS (JOINER)
        // ===================================================================
        public async Task ConnectToGameAsync(string hostIpAddress)
        {
            try
            {
                StopDiscovering();
                HostName remoteHost = new HostName(hostIpAddress);
                _gameSocket = new StreamSocket();
                await _gameSocket.ConnectAsync(remoteHost, TcpGamePort);
                Debug.WriteLine("Kapcsolódás sikeres! Várakozás a Host START üzenetére...");

                // --- Osztályszintű Olvasó/Író létrehozása ---
                _socketReader = new DataReader(_gameSocket.InputStream);
                _socketReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                _socketWriter = new DataWriter(_gameSocket.OutputStream);
                // ---

                StartReading();
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
        private async void StartReading()
        {
            _readCts = new CancellationTokenSource();
            try
            {
                while (!_readCts.IsCancellationRequested)
                {
                    uint size = await _socketReader.LoadAsync(sizeof(uint)).AsTask(_readCts.Token);
                    if (size == 0) throw new Exception("Kapcsolat lezárva (0 bájt olvasva).");

                    uint messageLength = _socketReader.ReadUInt32();
                    uint actualMessageSize = await _socketReader.LoadAsync(messageLength).AsTask(_readCts.Token);
                    if (actualMessageSize == 0 && messageLength > 0) throw new Exception("Kapcsolat lezárva (üzenet olvasása közben).");

                    if (actualMessageSize == messageLength)
                    {
                        string message = _socketReader.ReadString(actualMessageSize);
                        HandleReceivedMessage(message);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // VÁRT VISELKEDÉS (Amikor mi magunk hívjuk a Disconnect()-et)
                Debug.WriteLine("StartReading: Feladat szándékosan leállítva (Cancel).");
            }
            catch (Exception ex)
            {
                // Bármilyen más hiba (pl. "forcibly closed", ObjectDisposed)
                // azt jelenti, hogy a kapcsolat megszakadt.

                if (_readCts.IsCancellationRequested)
                {
                    Debug.WriteLine($"StartReading: Feladat szándékosan leállítva (Kivétel: {ex.GetType().Name}).");
                }
                else
                {
                    Debug.WriteLine($"Olvasási hiba (Váratlan): {ex.Message}");
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
                    GameStarted?.Invoke(this, new GameStartedEventArgs(opponentName, boardSize, false)); // Kliens = false
                }
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
                // Az ellenfél visszavágót kért.
                RematchReceived?.Invoke(this, EventArgs.Empty);
            }
            else if (message.StartsWith("LEAVE;"))
            {
                // A másik fél jelezte, hogy kilép (ő vár az ACK-ra).
                OpponentLeft?.Invoke(this, EventArgs.Empty);

                // DINAMIKUS VÁLASZ: Azonnal küldünk egy nyugtát ("LEAVE_ACK;")
                Task.Run(async () => await SendLeaveAckAsync());
            }
            else if (message.StartsWith("LEAVE_ACK;"))
            {
                // A másik fél nyugtázta, hogy vette a kilépési szándékunkat.
                LeaveAcknowledged?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task SendLeaveGameAsync()
        {
            // A SendMessageAsync már kezeli a hibákat (ObjectDisposed, stb.)
            await SendMessageAsync("LEAVE;");
            Debug.WriteLine("[NETWORK SEND] Leave üzenet küldve.");
        }

        public async Task SendRematchRequestAsync()
        {
            await SendMessageAsync("REMATCH;");
        }

        public async Task InitiateNetworkGameStartAsync(int boardSize)
        {
            if (_gameSocket == null)
            {
                Debug.WriteLine("Hiba: Játék indítása hívva, de nincs aktív TCP kapcsolat.");
                NetworkErrorOccurred?.Invoke(this, "Hiba: Nincs kapcsolat az ellenféllel.");
                return;
            }
            try
            {
                string hostName = "Host";
                string message = $"START;{hostName};{boardSize}";
                await SendMessageAsync(message);
                Debug.WriteLine($"[HOST SEND] Start üzenet küldve: {message}");

                string opponentName = _gameSocket.Information.RemoteHostName.DisplayName ?? "Kliens";
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

        private async Task SendMessageAsync(string message)
        {
            if (_socketWriter == null) // A _socketWriter-t ellenőrizzük
            {
                Debug.WriteLine("SendMessageAsync: Socket Writer nincs inicializálva, küldés kihagyva.");
                throw new InvalidOperationException("Socket Writer nincs inicializálva, küldés kihagyva.");
            }
            try
            {
                _socketWriter.WriteUInt32(_socketWriter.MeasureString(message));
                _socketWriter.WriteString(message);
                await _socketWriter.StoreAsync();
                await _socketWriter.FlushAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba az üzenet küldésekor: {ex.Message}");
                throw new Exception("Hiba az üzenet küldésekor", ex);
            }
        }

        public async Task SendLeaveAckAsync()
        {
            // A SendMessageAsync már kezeli a hibákat (ObjectDisposed, stb.)
            try
            {
                await SendMessageAsync("LEAVE_ACK;");
                Debug.WriteLine("[NETWORK SEND] Leave ACK (nyugta) küldve.");
            }
            catch (Exception ex)
            {
                // Ha a nyugta küldése hibát dob, az már nem baj,
                // mert a kapcsolat valószínűleg már bontva van.
                Debug.WriteLine($"Hiba a 'Leave ACK' küldésekor: {ex.Message}");
            }
        }

        // ===================================================================
        // TAKARÍTÁS ÉS IP-Lekérdezés
        // ===================================================================
        public void Disconnect()
        {
            _readCts?.Cancel(); // Leállítja a StartReading ciklust

            // Takarítjuk az új mezőket
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
        }

        public void Dispose()
        {
            StopHosting();
            StopDiscovering();
            Disconnect();
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