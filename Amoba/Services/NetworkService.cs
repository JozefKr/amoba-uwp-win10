using System;
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
    // Megjegyzés: GameFoundEventArgs, GameStartedEventArgs az INetworkService.cs-ben van definiálva.
    public class NetworkService : INetworkService, IDisposable
    {
        // --- Események (INetworkService) ---
        public event EventHandler<GameFoundEventArgs> GameFound;
        public event EventHandler<GameStartedEventArgs> GameStarted;
        public event EventHandler<int> MoveReceived;
        public event EventHandler OpponentDisconnected;
        public event EventHandler<string> NetworkErrorOccurred; // Hibaesemény a ViewModel felé
        public event EventHandler HostConnectionEstablished; // Jelzi a Host MainViewModel-nek, hogy navigálhat
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

        private DataReader _socketReader;
        private DataWriter _socketWriter;

        // --- Konstruktor ---
        public NetworkService()
        {
            try
            {
                _multicastHostName = new HostName(MulticastGroupAddress);
            }
            catch (ArgumentException)
            {
                Debug.WriteLine("Érvénytelen multicast cím formátum!");
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
                    TimeSpan.FromSeconds(2)
                );

                Debug.WriteLine(
                    $"Hostolás elindítva, küldés a {MulticastGroupAddress}:{UdpPort} címre."
                );

                // Aszinkron jelzés befejezése

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // Mivel ez egy hálózati hiba, a kivételt tovább kell dobnunk a ViewModel felé.

                System.Diagnostics.Debug.WriteLine(
                    $"KRITIKUS HIBA AZ UDP INDÍTÁSAKOR: {ex.Message}"
                );

                StopHosting();

                throw; // A ViewModel catch blokkja elkapja ezt
            }
        }

        // ===================================================================
        // JÁTÉK INDÍTÁSA A HOST OLDALÁRÓL (EZT HÍVJA A GAMESIZEVIEWMODEL)
        // ===================================================================
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
                // 1. ÜZENET KÜLDÉSE A KLIENSNEK: START;HostNeve;Méret
                // (A kód, ami korábban az '_INTERNAL' metódusban volt, most itt van)
                string hostName = GetLocalIpAddress() ?? "Host";
                string message = $"START;{hostName};{boardSize}";
                await SendMessageAsync(message);
                Debug.WriteLine($"[HOST SEND] Start üzenet küldve: {message}");

                // 2. LOKÁLIS ESEMÉNY KIVÁLTÁSA A HOST GAMEVIEWMODEL SZÁMÁRA
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

        private async Task SendBroadcastMessage(string playerName)
        {
            if (_hostSocket == null || _multicastHostName == null)
                return;

            try
            {
                string localIp = GetLocalIpAddress() ?? "UnknownIP";

                string message = $"{BroadcastMessagePrefix}{playerName};{localIp}";

                using (
                    var stream = await _hostSocket.GetOutputStreamAsync(_multicastHostName, UdpPort)
                )

                using (var writer = new DataWriter(stream))
                {
                    byte[] data = Encoding.UTF8.GetBytes(message);

                    writer.WriteBytes(data);

                    await writer.StoreAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba az üzenetküldéskor: {ex.Message}");
            }
        }

        public void StopHosting()
        {
            _broadcastTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            _broadcastTimer?.Dispose();

            _broadcastTimer = null;

            _hostSocket?.Dispose();

            _hostSocket = null;

            Debug.WriteLine("Hostolás leállítva.");
        }

        // ===================================================================

        // JÁTÉK KERESÉSE (UDP FOGADÁS)

        // ===================================================================

        public async Task StartDiscoveringAsync()
        {
            if (_listenerSocket != null || _multicastHostName == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Keresés már fut, vagy HostName inicializálása sikertelen."
                );

                return;
            }

            try
            {
                _listenerSocket = new DatagramSocket();

                _listenerSocket.MessageReceived += ListenerSocket_MessageReceived;

                // A HELYES WINRT SORREND: 1. Bindolás, 2. Join

                await _listenerSocket.BindServiceNameAsync(UdpPort);

                _listenerSocket.JoinMulticastGroup(_multicastHostName);

                System.Diagnostics.Debug.WriteLine(
                    $"Keresés elindítva, figyelés a(z) {MulticastGroupAddress}:{UdpPort} címen..."
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hiba a keresés indításakor: {ex.Message}");

                StopDiscovering();

                // Hibajelzés a ViewModel felé

                NetworkErrorOccurred?.Invoke(
                    this,
                    $"Hiba a keresés indításakor: {ex.Message}. (Lehet, hogy a port foglalt, vagy tűzfal blokkolja.)"
                );
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

        private void ListenerSocket_MessageReceived(
            DatagramSocket sender,
            DatagramSocketMessageReceivedEventArgs args
        )
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
                        System.Diagnostics.Debug.WriteLine(
                            "[NETWORK WARNING] Üres UDP csomag érkezett. Figyelmen kívül hagyva."
                        );

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
                                System.Diagnostics.Debug.WriteLine(
                                    $"[SELF HOST] Saját üzenet fogadva: {ipAddress}. Szűrve."
                                );

                                return;
                            }

                            GameFound?.Invoke(this, new GameFoundEventArgs(playerName, ipAddress));

                            System.Diagnostics.Debug.WriteLine(
                                $"Játék találat: {playerName} ({ipAddress})"
                            );
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[UNKNOWN MSG] Ismeretlen formátumú UDP üzenet: '{message}'"
                        );
                    }
                }
                catch (Exception ex)
                {
                    // Hibát kaphatunk a reader.ReadString-nél, ha az adat korrupt

                    System.Diagnostics.Debug.WriteLine(
                        $"KRITIKUS HIBA az üzenetfeldolgozásban: {ex.Message}"
                    );
                }
            }
        }

        // ===================================================================
        // TCP KAPCSOLAT FOGADÁSA (HOST)
        // ===================================================================

        public async Task StartAcceptingConnectionsAsync()
        {
            Debug.WriteLine("TCP LISTENER INDÍTÁSA...");
            if (_tcpListener != null)
                return;
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
                _tcpListener?.Dispose();
                _tcpListener = null;
                throw; // Dobjuk vissza a kivételt a ViewModel felé
            }
        }

        private void TcpListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            // 1. Csak egy kapcsolatot fogadunk el
            if (_gameSocket != null)
            {
                args.Socket.Dispose();
                return;
            }

            _gameSocket = args.Socket;
            Debug.WriteLine($"Kapcsolat elfogadva: {_gameSocket.Information.RemoteHostName}");

            // 2. Leállítjuk a további figyelést (már van ellenfél)
            _tcpListener?.Dispose();
            _tcpListener = null;

            // 3. Létrehozzuk az olvasót és írót (ahogy a legutóbbi javításban)
            _socketReader = new DataReader(_gameSocket.InputStream);
            _socketReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            _socketWriter = new DataWriter(_gameSocket.OutputStream);

            // 4. Elindítjuk a háttérben az üzenetek figyelését
            StartReading();

            // 5. JELZÜNK A MAINVIEWMODEL-NEK (A LÉNYEG)
            // Nem küldünk "START"-ot, csak jelzünk a Host UI-nak, hogy navigálhat.
            HostConnectionEstablished?.Invoke(this, EventArgs.Empty);
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

                // --- JAVÍTÁS: Olvasó és Író Létrehozása ---
                _socketReader = new DataReader(_gameSocket.InputStream);
                _socketReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                _socketWriter = new DataWriter(_gameSocket.OutputStream);
                // ---

                StartReading(); // Már nem kell átadni a socketet
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
                // JAVÍTÁS: A 'using' blokk eltávolítva!
                // var reader = _socketReader; // Csak egy referencia

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
                Debug.WriteLine("StartReading: Feladat szándékosan leállítva.");
            }
            catch (Exception ex) when (_readCts != null && !_readCts.IsCancellationRequested)
            {
                Debug.WriteLine($"Olvasási hiba: {ex.Message}");
                OpponentDisconnected?.Invoke(this, EventArgs.Empty);
                Disconnect();
            }
        }

        private void HandleReceivedMessage(string message)
        {
            // A KLIENS OLDAL FOGADJA A START ÜZENETET

            if (message.StartsWith("START;"))
            {
                string[] parts = message.Split(';');

                if (parts.Length >= 3 && int.TryParse(parts[2], out int boardSize))
                {
                    string opponentName = parts[1];

                    // KIVÁLTJUK A GameStarted ESEMÉNYT A KLIENS GAMEVIEWMODEL SZÁMÁRA

                    GameStarted?.Invoke(
                        this,
                        new GameStartedEventArgs(opponentName, boardSize, false)
                    ); // Kliens = false
                }
            }
            else if (message.StartsWith("MOVE:")) // Lépés fogadása változatlan
            {
                if (int.TryParse(message.Substring(5), out int moveIndex))
                {
                    MoveReceived?.Invoke(this, moveIndex);
                }
            }
            // A SIZE; üzenet már nem létezik

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
            if (_socketWriter == null) //A writert ellenőrizzük
            {
                Debug.WriteLine("SendMessageAsync: Socket Writer nincs inicializálva, küldés kihagyva.");
                return;
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
                // JAVÍTÁS: Továbbdobjuk a hibát a ViewModel felé
                throw new Exception("Hiba az üzenet küldésekor", ex);
            }
        }

        // ===================================================================

        // TAKARÍTÁS ÉS IP-Lekérdezés

        // ===================================================================

        public void Disconnect()
        {
            _readCts?.Cancel(); // Leállítja a StartReading ciklust

            // Takarítsuk az új mezőket
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

            if (profile?.NetworkAdapter == null)
                return null;

            var hostNames = NetworkInformation.GetHostNames();

            foreach (var hn in hostNames)
            {
                if (
                    hn.IPInformation?.NetworkAdapter != null
                    && hn.IPInformation.NetworkAdapter.NetworkAdapterId
                        == profile.NetworkAdapter.NetworkAdapterId
                    && hn.Type == HostNameType.Ipv4
                )
                {
                    return hn.CanonicalName;
                }
            }
            return null;
        }
    }
}