using GalaSoft.MvvmLight.Threading;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Collections.Generic;

namespace Amoba.Services
{
    // Megjegyzés: GameFoundEventArgs, GameStartedEventArgs az INetworkService.cs-ben van definiálva.

    public class NetworkService : INetworkService, IDisposable
    {
        // --- Események (INetworkService) ---
        public event EventHandler<GameFoundEventArgs> GameFound;
        public event EventHandler<GameStartedEventArgs> GameStarted;
        public event EventHandler<GameStartedEventArgs> HostGameReady;
        public event EventHandler<int> MoveReceived;
        public event EventHandler OpponentDisconnected;
        public event EventHandler<string> NetworkErrorOccurred; // Hibaesemény a ViewModel felé
        public event EventHandler<int> BoardSizeReceived;

        // --- Konstansok és Mezők ---
        private const string MulticastGroupAddress = "239.255.42.99";
        private const string UdpPort = "9090";
        private const string TcpGamePort = "9091"; // TCP port a játékhoz
        private const string BroadcastMessagePrefix = "AMOBA_GAME_HOST;";

        private DatagramSocket _hostSocket;
        private DatagramSocket _listenerSocket;
        private StreamSocketListener _tcpListener;
        private StreamSocket _gameSocket;
        private HostName _multicastHostName;
        private Timer _broadcastTimer;
        private CancellationTokenSource _readCts;

        // --- Konstruktor ---
        public NetworkService()
        {
            try
            {
                _multicastHostName = new HostName(MulticastGroupAddress);
            }
            catch (ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine("Érvénytelen multicast cím formátum!");
            }
        }

        // ===================================================================
        // HOSTOLÁS (UDP KÜLDÉS)
        // ===================================================================

        public async Task StartHostingAsync(string playerName)
        {
            if (_multicastHostName == null || _broadcastTimer != null || _hostSocket != null)
            {
                await Task.CompletedTask;
                return;
            }

            try
            {
                _hostSocket = new DatagramSocket();

                // FONTOS: Explicit módon lefoglaljuk a portot a helyi gépen.
                // Ha ez hibázik, kivételt dob, és a ViewModel elkapja!
                // Ez volt a kulcs a korábbi, sikeres tesztekhez.
                await _hostSocket.BindServiceNameAsync(UdpPort); // <-- VISSZAHOZVA

                // Timer beállítása
                _broadcastTimer = new Timer(
                    async (state) => await SendBroadcastMessage(playerName),
                    null,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2));

                System.Diagnostics.Debug.WriteLine($"Hostolás elindítva, küldés a {MulticastGroupAddress}:{UdpPort} címre.");

                // Aszinkron jelzés befejezése
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // Mivel ez egy hálózati hiba, a kivételt tovább kell dobnunk a ViewModel felé.
                System.Diagnostics.Debug.WriteLine($"KRITIKUS HIBA AZ UDP INDÍTÁSAKOR: {ex.Message}");
                StopHosting();
                throw; // A ViewModel catch blokkja elkapja ezt
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hiba az üzenetküldéskor: {ex.Message}");
            }
        }

        public void StopHosting()
        {
            _broadcastTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _broadcastTimer?.Dispose();
            _broadcastTimer = null;

            _hostSocket?.Dispose();
            _hostSocket = null;
            System.Diagnostics.Debug.WriteLine("Hostolás leállítva.");
        }


        // ===================================================================
        // JÁTÉK KERESÉSE (UDP FOGADÁS)
        // ===================================================================

        public async Task StartDiscoveringAsync()
        {
            if (_listenerSocket != null || _multicastHostName == null)
            {
                System.Diagnostics.Debug.WriteLine("Keresés már fut, vagy HostName inicializálása sikertelen.");
                return;
            }

            try
            {
                _listenerSocket = new DatagramSocket();
                _listenerSocket.MessageReceived += ListenerSocket_MessageReceived;

                // A HELYES WINRT SORREND: 1. Bindolás, 2. Join
                await _listenerSocket.BindServiceNameAsync(UdpPort);
                _listenerSocket.JoinMulticastGroup(_multicastHostName);

                System.Diagnostics.Debug.WriteLine($"Keresés elindítva, figyelés a(z) {MulticastGroupAddress}:{UdpPort} címen...");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hiba a keresés indításakor: {ex.Message}");
                StopDiscovering();
                // Hibajelzés a ViewModel felé
                NetworkErrorOccurred?.Invoke(this, $"Hiba a keresés indításakor: {ex.Message}. (Lehet, hogy a port foglalt, vagy tűzfal blokkolja.)");
            }
        }

        public void StopDiscovering()
        {
            if (_listenerSocket != null)
            {
                _listenerSocket.MessageReceived -= ListenerSocket_MessageReceived;
                _listenerSocket.Dispose();
                _listenerSocket = null;
                System.Diagnostics.Debug.WriteLine("Keresés leállítva.");
            }
        }

        private void ListenerSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            // === DEBUG PONT: ITT TUDJUK, HOGY AZ ESEMÉNY LEFUTOTT ===
            System.Diagnostics.Debug.WriteLine($"[NETWORK] Üzenet esemény érkezett.");

            // FONTOS: Olvasás a bejövő Stream-ből
            using (DataReader reader = args.GetDataReader())
            {
                try
                {
                    uint unreadBytes = reader.UnconsumedBufferLength;

                    // JAVÍTÁS: Ellenőrizzük, hogy van-e egyáltalán olvasatlan adat!
                    if (unreadBytes == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("[NETWORK WARNING] Üres UDP csomag érkezett. Figyelmen kívül hagyva.");
                        return; // Kilépünk
                    }

                    // Üzenet beolvasása és tisztítása
                    string message = reader.ReadString(unreadBytes);

                    // TISZTÍTÁS: (A korábbi javaslatunk a null karakterek miatt)
                    message = message.Trim('\0').Trim();

                    // === DEBUG PONT 2: ITT LÁTJUK, HOGY MIT FOGADOTT ===
                    System.Diagnostics.Debug.WriteLine($"[NETWORK RECEIVED] Tartalom: '{message}'");


                    if (message.StartsWith(BroadcastMessagePrefix))
                    {
                        // ... (a logika a GameFound esemény kiváltására) ...
                        string[] parts = message.Split(';');

                        if (parts.Length >= 3)
                        {
                            string playerName = parts[1];
                            string ipAddress = args.RemoteAddress.CanonicalName;

                            string myIp = GetLocalIpAddress();
                            if (myIp != null && ipAddress == myIp)
                            {
                                System.Diagnostics.Debug.WriteLine($"[SELF HOST] Saját üzenet fogadva: {ipAddress}. Szűrve.");
                                return;
                            }

                            GameFound?.Invoke(this, new GameFoundEventArgs(playerName, ipAddress));
                            System.Diagnostics.Debug.WriteLine($"Játék találat: {playerName} ({ipAddress})");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[UNKNOWN MSG] Ismeretlen formátumú UDP üzenet: '{message}'");
                    }
                }
                catch (Exception ex)
                {
                    // Hibát kaphatunk a reader.ReadString-nél, ha az adat korrupt
                    System.Diagnostics.Debug.WriteLine($"KRITIKUS HIBA az üzenetfeldolgozásban: {ex.Message}");
                }
            }
        }


        // ===================================================================
        // TCP KAPCSOLAT FOGADÁSA (HOST)
        // ===================================================================

        public async Task StartAcceptingConnectionsAsync()
        {
            System.Diagnostics.Debug.WriteLine("TCP LISTENER INDÍTÁSA...");
            //StopHosting(); // Leállítjuk az UDP hirdetést
            if (_tcpListener != null) return;

            try
            {
                _tcpListener = new StreamSocketListener();
                _tcpListener.ConnectionReceived += TcpListener_ConnectionReceived;
                await _tcpListener.BindServiceNameAsync(TcpGamePort);
                System.Diagnostics.Debug.WriteLine($"TCP Listener aktív a porton: {TcpGamePort}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hiba a TCP listener indításakor: {ex.Message}");
                _tcpListener?.Dispose();
                _tcpListener = null;
                throw; // Dobjuk vissza a kivételt a ViewModel felé
            }
        }

        private async void TcpListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            if (_gameSocket != null)
            {
                args.Socket.Dispose();
                return;
            }

            _gameSocket = args.Socket;
            System.Diagnostics.Debug.WriteLine($"Kapcsolat elfogadva: {_gameSocket.Information.RemoteHostName}");

            _tcpListener.Dispose();
            _tcpListener = null;

            // 1. ÜZENET: Küldjük el a játék kezdő adatait a kliensnek.
            //await SendGameStartDataAsync(args.Socket, "Host Játékos", 3, true);

            StartReading(_gameSocket);

            // KIVÁLTJUK AZ ESEMÉNYT: A játék indul!
            // Ez az esemény jelzi a Host MainViewModel-nek, hogy navigálhat a GamePage-re.
            HostGameReady?.Invoke(this, new GameStartedEventArgs(args.Socket.Information.RemoteHostName.DisplayName, 3, true));
        }

        // Segédmetódus a kezdő üzenet küldésére (Host -> Joiner)
        /*
        private async Task SendGameStartDataAsync(StreamSocket socket, string opponentName, int boardSize, bool isHost)
        {
            try
            {
                // START;OpponentName;BoardSize;IS_HOST(true/false)
                string message = $"START;{opponentName};{boardSize};{(isHost ? "HOST" : "CLIENT")}";
                using (var writer = new DataWriter(socket.OutputStream))
                {
                    writer.WriteUInt32(writer.MeasureString(message));
                    writer.WriteString(message);
                    await writer.StoreAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hiba a START üzenet küldésekor: {ex.Message}");
                Disconnect();
            }
        }
        */

        // ===================================================================
        // TCP KAPCSOLÓDÁS (JOINER)
        // ===================================================================

        public async Task<bool> ConnectToGameAsync(string hostIpAddress)
        {
            try
            {
                StopDiscovering();

                HostName remoteHost = new HostName(hostIpAddress);
                _gameSocket = new StreamSocket();

                await _gameSocket.ConnectAsync(remoteHost, TcpGamePort);
                System.Diagnostics.Debug.WriteLine("Kapcsolódás sikeres!");

                StartReading(_gameSocket);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hiba a kapcsolódáskor: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        // ===================================================================
        // KOMMUNIKÁCIÓ ÉS LÉPÉSEK
        // ===================================================================

        private async void StartReading(StreamSocket socket)
        {
            _readCts = new CancellationTokenSource();
            try
            {
                using (var reader = new DataReader(socket.InputStream))
                {
                    reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

                    while (!_readCts.IsCancellationRequested)
                    {
                        uint size = await reader.LoadAsync(sizeof(uint)).AsTask(_readCts.Token);
                        if (size == 0) throw new Exception("Kapcsolat megszakadt.");

                        uint messageLength = reader.ReadUInt32();
                        uint actualMessageSize = await reader.LoadAsync(messageLength).AsTask(_readCts.Token);

                        if (actualMessageSize == messageLength)
                        {
                            string message = reader.ReadString(actualMessageSize);
                            HandleReceivedMessage(message);
                        }
                    }
                }
            }
            catch (Exception ex) when (_readCts.IsCancellationRequested == false)
            {
                System.Diagnostics.Debug.WriteLine($"Olvasási hiba: {ex.Message}");
                OpponentDisconnected?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                Disconnect();
            }
        }

        private void HandleReceivedMessage(string message)
        {
            // A hívó fél (Host) elküldi a START üzenetet, miután a méretet elküldte, 
            // hogy elindítsa a GameViewModel-t.

            if (message.StartsWith("START;"))
            {
                // JÁTÉK INDÍTÁSA (Host küldi a Joinernek)
                string[] parts = message.Split(';');
                if (parts.Length >= 4)
                {
                    string opponentName = parts[1];
                    int boardSize = int.Parse(parts[2]);
                    bool isHost = (parts[3] == "HOST");

                    // A Kliens itt kapja meg az adatokat a Hosttól.
                    // Ezzel aktiválja a GameViewModel.NetworkService_GameStarted eseményt.
                    GameStarted?.Invoke(this, new GameStartedEventArgs(opponentName, boardSize, false)); // A Joiner a Client!
                }
            }
            // ÚJ LOGIKA: Méretfogadás
            else if (message.StartsWith("SIZE;"))
            {
                // Kliens fogadja a Host által választott táblaméretetNetworkService_HostGameReady
                string[] parts = message.Split(';');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int boardSize))
                {
                    // Kiváltjuk az eseményt a MainViewModel felé (ennek BoardSizeReceived eseménye van!)
                    // Mivel a MainViewModel felel a GameSizePage->GamePage navigációért,
                    // ő fog reagálni erre az eseményre, és navigál.
                    BoardSizeReceived?.Invoke(this, boardSize);
                }
            }
            else if (message.StartsWith("MOVE:"))
            {
                // LÉPÉS ÉRKEZETT
                if (int.TryParse(message.Substring(5), out int moveIndex))
                {
                    MoveReceived?.Invoke(this, moveIndex);
                }
            }
            // TODO: Egyéb üzenetek, pl. "RESET", "WINNER"
        }

        public async Task SendBoardSizeAsync(int boardSize)
        {
            if (_gameSocket != null)
            {
                // 1. LÉPÉS: Méret elküldése
                string sizeMessage = $"SIZE;{boardSize}";
                await SendMessageAsync(sizeMessage);
                System.Diagnostics.Debug.WriteLine($"[HOST SEND] Elküldött méret: {sizeMessage}");

                // 2. LÉPÉS: Játék indítási parancs elküldése (a kliens feliratkozott a GameStarted eseményre)
                string startMessage = $"START;{boardSize};{true}"; // Host (X) kezdi
                await SendMessageAsync(startMessage);
                System.Diagnostics.Debug.WriteLine($"[HOST SEND] Elküldött start: {startMessage}");
            }
        }

        public async Task SendMoveAsync(int index)
        {
            string moveMessage = $"MOVE:{index}";
            await SendMessageAsync(moveMessage);
        }

        private async Task SendMessageAsync(string message)
        {
            if (_gameSocket == null) return;

            try
            {
                using (var writer = new DataWriter(_gameSocket.OutputStream))
                {
                    writer.WriteUInt32(writer.MeasureString(message));
                    writer.WriteString(message);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hiba az üzenet küldésekor: {ex.Message}");
                Disconnect();
            }
        }


        // ===================================================================
        // TAKARÍTÁS ÉS IP-Lekérdezés
        // ===================================================================

        public void Disconnect()
        {
            _readCts?.Cancel();
            _tcpListener?.Dispose();
            _tcpListener = null;

            if (_gameSocket != null)
            {
                _gameSocket.Dispose();
                _gameSocket = null;
                System.Diagnostics.Debug.WriteLine("TCP kapcsolat megszakadt.");
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