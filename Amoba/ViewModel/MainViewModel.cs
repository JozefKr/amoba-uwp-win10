using Amoba.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autofac.Core;
using Autofac;
using Windows.Storage;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI;
using Amoba.Model;

namespace Amoba.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IViewService _viewService;
        private readonly INetworkService _networkService;

        // --- Időzítő a lejárt hostok eltávolításához ---
        private DispatcherTimer _discoveryCleanupTimer;
        private const int StaleGameTimeoutSeconds = 5; // Host 2 másodpercenként küld, 5s csend után lejártnak tekintjük

        private static readonly Brush ErrorBrush = new SolidColorBrush(Colors.Red);
        private static readonly Brush InfoBrush = new SolidColorBrush(Colors.Green);

        /// <summary>
        /// Igaz, ha a ViewModel éppen egy másik oldalra navigál.
        /// </summary>
        private bool _isNavigatingAway = false;

        // --- Privát Állapotjelzők ---
        private bool _isJoiningGame = false;
        public bool IsJoiningGame
        {
            get => _isJoiningGame;
            set
            {
                if (Set(ref _isJoiningGame, value))
                {
                    RaisePropertyChanged(nameof(IsNameEntryEnabled));
                    RaisePropertyChanged(nameof(IsInMenu));
                    (JoinGameCommand as RelayCommand<DiscoveredGame>)?.RaiseCanExecuteChanged();
                    (CancelJoinCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StartLocalPvpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StartAiGameCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        private bool _isSearching = false;
        private string _statusMessage = string.Empty;
        private const string PlayerNameSettingsKey = "PlayerName";
        private string _playerName;
        public string PlayerName
        {
            get => _playerName;
            set
            {
                // Csak akkor fut le, ha az érték tényleg változott
                if (Set(ref _playerName, value))
                {
                    // A változás azonnali mentése
                    SavePlayerName(value);

                    RaisePropertyChanged(nameof(IsNetworkReady));
                    (StartHostingCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StartDiscoveryCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (JoinGameCommand as RelayCommand<DiscoveredGame>)?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isHosting = false;
        public bool IsHosting
        {
            get => _isHosting;
            set
            {
                if (Set(ref _isHosting, value))
                {
                    RaisePropertyChanged(nameof(IsNameEntryEnabled));
                    RaisePropertyChanged(nameof(IsInMenu));

                    (StartLocalPvpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StartAiGameCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // --- Publikus Tulajdonságok a UI-hoz ---
        public ObservableCollection<DiscoveredGame> FoundGames { get; } = new ObservableCollection<DiscoveredGame>();

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (Set(ref _isSearching, value))
                {
                    RaisePropertyChanged(nameof(IsNameEntryEnabled));
                    RaisePropertyChanged(nameof(IsInMenu));

                    (StartLocalPvpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StartAiGameCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Igaz, ha a név-beviteli mezőnek engedélyezve kell lennie.
        /// </summary>
        public bool IsNameEntryEnabled => !IsHosting && !IsSearching && !IsJoiningGame;

        /// <summary>
        /// Igaz, ha a fő hálózati gomboknak (Host, Keresés) látszódniuk kell.
        /// </summary>
        public bool IsInMenu => !IsHosting && !IsSearching && !IsJoiningGame;

        public string StatusMessage
        {
            get => _statusMessage;
            private set => Set(ref _statusMessage, value);
        }

        private Brush _statusMessageBrush;
        public Brush StatusMessageBrush
        {
            get => _statusMessageBrush;
            set => Set(ref _statusMessageBrush, value);
        }

        /// <summary>
        /// Igaz, ha a hálózati gomboknak engedélyezve kell lenniük.
        /// </summary>
        public bool IsNetworkReady => !string.IsNullOrWhiteSpace(PlayerName);

        // ===================================================================
        // --- PARANCS MEZŐK ---
        // ===================================================================

        public ICommand StartLocalPvpCommand { get; private set; }
        public ICommand StartAiGameCommand { get; private set; }

        public ICommand StartHostingCommand { get; private set; }
        public ICommand StartDiscoveryCommand { get; private set; }
        public ICommand StopDiscoveryCommand { get; private set; }
        public ICommand JoinGameCommand { get; private set; }
        public ICommand StopHostingCommand { get; private set; }
        public ICommand CancelJoinCommand { get; private set; }


        // ===================================================================
        // --- KONSTRUKTOR ---
        // ===================================================================

        public MainViewModel(IViewService viewService, INetworkService networkService)
        {
            _viewService = viewService;
            _networkService = networkService;

            LoadPlayerName();

            // Feliratkozás a hálózati eseményekre
            _networkService.GameFound += NetworkService_GameFound;
            _networkService.NetworkErrorOccurred += NetworkService_NetworkErrorOccurred;
            _networkService.HostConnectionEstablished += NetworkService_HostConnectionEstablished;
            _networkService.GameStarted += NetworkService_GameStarted;

            // Alapértelmezett szín beállítása
            StatusMessageBrush = InfoBrush;

            InitializeCommands();

            // === Lejárt játékok figyelőjének indítása ===
            _discoveryCleanupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // Másodpercenként fut
            };
            _discoveryCleanupTimer.Tick += CleanupStaleGames_Tick;
        }

        // ===================================================================
        // --- Parancsok inicializálása ---
        // ===================================================================
        private void InitializeCommands()
        {
            // Helyi parancsok
            StartLocalPvpCommand = new RelayCommand(StartLocalPvpMethod, () => IsInMenu);
            StartAiGameCommand = new RelayCommand(StartAiGameMethod, () => IsInMenu);

            // Hálózati parancsok
            StartHostingCommand = new RelayCommand(ExecuteStartHosting, () => IsNetworkReady);
            StartDiscoveryCommand = new RelayCommand(StartDiscovery, () => IsNetworkReady);
            StopDiscoveryCommand = new RelayCommand(StopDiscovery);
            JoinGameCommand = new RelayCommand<DiscoveredGame>(ExecuteJoinGame, p => p != null && !IsJoiningGame && IsNetworkReady);

            StopHostingCommand = new RelayCommand(ExecuteStopHosting);
            CancelJoinCommand = new RelayCommand(ExecuteCancelJoin, () => IsJoiningGame);
        }

        /// <summary>
        /// Központilag beállítja a státuszüzenetet és annak színét.
        /// </summary>
        /// <param name="message">A megjelenítendő szöveg</param>
        /// <param name="type">Az üzenet típusa (Info = zöld, Error = piros)</param>
        public void SetStatus(string message, StatusType type = StatusType.Info)
        {
            StatusMessage = message; // Beállítjuk a szöveget

            // Beállítjuk a színt
            switch (type)
            {
                case StatusType.Error:
                    StatusMessageBrush = ErrorBrush;
                    break;

                // case StatusType.Warning:
                //    StatusMessageBrush = WarningBrush; //későbbiekhez
                //    break;

                case StatusType.Info:
                default:
                    StatusMessageBrush = InfoBrush;
                    break;
            }
        }

        private void SavePlayerName(string name)
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            settings.Values[PlayerNameSettingsKey] = name;
        }

        private void LoadPlayerName()
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.ContainsKey(PlayerNameSettingsKey))
            {
                _playerName = settings.Values[PlayerNameSettingsKey] as string;
            }
            else
            {
                _playerName = "Játékos";
            }
        }

        // ===================================================================
        // --- PARANCSOK IMPLEMENTÁCIÓJA ---
        // ===================================================================

        #region Helyi Játék

        /// <summary>
        /// Indítja a Helyi (Player vs Player) játékot
        /// </summary>
        private void StartLocalPvpMethod()
        {
            StopAllNetworkActivity();
            UnsubscribeFromNetworkEvents();

            var localParams = new List<Parameter>
            {
              new NamedParameter("isVsComputer", false),
              new NamedParameter("isNetworkGame", false)
            };
            _viewService.OpenPage<GameSizeViewModel>(localParams.ToArray());
        }

        /// <summary>
        /// Indítja a Gép Elleni (AI) játékot
        /// </summary>
        private void StartAiGameMethod()
        {
            StopAllNetworkActivity();
            UnsubscribeFromNetworkEvents();

            var aiParams = new List<Parameter>
            {
              new NamedParameter("isVsComputer", true),
              new NamedParameter("isNetworkGame", false)
            };
            _viewService.OpenPage<GameSizeViewModel>(aiParams.ToArray());
        }

        #endregion

        #region Hálózati Játék (Host és Kliens)

        // 2. JÁTÉK HOSTOLÁSA (SZERVER)
        private async void ExecuteStartHosting()
        {
            StopDiscovery();
            IsHosting = true;

            try
            {
                SetStatus("Játék hostolása folyamatban, TCP szerver indítása...", StatusType.Info);
                await _networkService.StartHostingAsync(PlayerName);
                await _networkService.StartAcceptingConnectionsAsync();
                SetStatus("Hostolás aktív! Várjuk a csatlakozást...", StatusType.Info);
            }
            catch (Exception ex)
            {
                SetStatus($"HIBA: Nem sikerült hostolni. {ex.Message}", StatusType.Error);
                _networkService.StopHosting();
                IsHosting = false;
            }
        }

        /// <summary>
        /// Leállítja a hostolást és visszaáll a menübe.
        /// </summary>
        private void ExecuteStopHosting()
        {
            _networkService.StopHosting();
            IsHosting = false;
            SetStatus("Hostolás leállítva.", StatusType.Info);
        }

        // 3. JÁTÉK KERESÉSE (KLIENS)
        private async void StartDiscovery()
        {
            if (IsSearching) return;
            _networkService.StopHosting();
            IsHosting = false;

            FoundGames.Clear();
            SetStatus("Játékok keresése aktív...", StatusType.Info);
            await _networkService.StartDiscoveringAsync();
            IsSearching = true;

            // === Időzítő elindítása ===
            _discoveryCleanupTimer.Start();
        }

        // 4. KERESÉS LEÁLLÍTÁSA
        private void StopDiscovery()
        {
            _networkService.StopDiscovering();
            IsSearching = false;
            SetStatus("Keresés leállítva.", StatusType.Info);

            // === Időzítő leállítása ===
            _discoveryCleanupTimer.Stop();
            FoundGames.Clear(); // Töröljük a listát, ha manuálisan állítjuk le
        }

        // 5. CSATLAKOZÁS A TALÁLT JÁTÉKHOZ (KLIENS)
        private async void ExecuteJoinGame(DiscoveredGame gameToJoin)
        {
            if (IsJoiningGame) return;
            IsJoiningGame = true;
            StopDiscovery();
            SetStatus($"Csatlakozás: {gameToJoin.IpAddress}...", StatusType.Info);

            await _networkService.ConnectToGameAsync(gameToJoin.IpAddress, PlayerName);

            // Ha az 'IsJoiningGame' még mindig 'true', akkor a csatlakozás SIKERES volt
            // (mert a 'NetworkErrorOccurred' nem futott le és nem állította 'false'-ra)
            if (IsJoiningGame)
            {
                // Ez a sor most már csak akkor fut le, ha a csatlakozás TÉNYLEG sikeres volt.
                SetStatus("Sikeresen csatlakozva. Várakozás a Hostra...", StatusType.Info);
                (CancelJoinCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            else
            {
                // Ha az 'IsJoiningGame' 'false', az azt jelenti, hogy a 'NetworkErrorOccurred' lefutott.
                Debug.WriteLine("ExecuteJoinGame: Csatlakozás sikertelen (NetworkErrorOccurred kezelte).");

                // 1. Nem vizsgáljuk a technikai hibaüzenetet,
                //    hanem egy rövid, egységes üzenetet adunk.
                string friendlyErrorMessage = "A csatlakozás sikertelen. A host már nem elérhető, vagy leállította a játékot.";

                // 2. Jelenítsük meg a felugró ablakot (UI szálon)
                // Mivel ez egy async void metódus, itt biztonságos await-et használni
                await DispatcherHelper.RunAsync(async () =>
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Csatlakozás Sikertelen",
                        Content = friendlyErrorMessage,
                        PrimaryButtonText = "OK"
                    };
                    await dialog.ShowAsync();
                });

                // 4. A dialógus bezárása után indítjuk újra a keresést
                StartDiscovery();
            }
        }

        /// <summary>
        /// A "Mégse" gomb parancsa, amikor a felhasználó éppen egy játékhoz
        /// próbál csatlakozni ("Csatlakozás..." képernyő).
        /// </summary>
        private async void ExecuteCancelJoin()
        {
            if (_networkService == null)
            {
                Debug.WriteLine("ExecuteCancelJoin: _networkService null, nincs mit megszakítani.");
                return; // <-- Lépj ki, HA NINCS networkService
            }

            try
            {
                // 1. Központi megszakító metódus hívása.
                //    Ez automatikusan elküldi a "CANCEL_WAIT" üzenetet (ha van kapcsolat)
                //    és leállít MINDEN hálózati tevékenységet (TCP, UDP host, UDP discovery).
                await _networkService.CancelAllOperationsAsync();

                // 2. Visszaállítjuk az állapotokat
                IsJoiningGame = false;

                // 3. StatusMessage frissítése
                SetStatus("Csatlakozás megszakítva. Keresés újraindítva.", StatusType.Info);

                // 4. Újraindítjuk a keresést, hogy a többi játék látszódjon
                //    (A StartDiscovery() elindítja az időzítőt is)
                StartDiscovery();

                // 5. A CanExecute frissítése, hogy a JoinGameCommand is reagáljon
                (JoinGameCommand as RelayCommand<DiscoveredGame>)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)

            {
                // Ha a megszakítás során hiba történik, naplózzuk és
                // egy biztonságos állapotba lépünk (pl. Főmenü).
                Debug.WriteLine($"Váratlan hiba a csatlakozás megszakításakor: {ex.Message}");
                SetStatus("Hiba történt a megszakítás során. Visszatérés a főmenübe.", StatusType.Error);
                IsJoiningGame = false;
            }
        }
        #endregion

        // ===================================================================
        // --- ESEMÉNYKEZELŐK (A NetworkService-től) ---
        // ===================================================================

        private void NetworkService_GameFound(object sender, GameFoundEventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // 1. Megkeressük, hogy létezik-e már ez a játék (IP cím alapján)
                var existingGame = FoundGames.FirstOrDefault(g => g.IpAddress == e.IpAddress);
                string newName = $"{e.HostName} játéka";

                if (existingGame != null)
                {
                    // 2. HA IGEN: Csak az időbélyeget frissítjük
                    existingGame.LastSeen = DateTime.Now;

                    // Opcionális: Frissítsük a nevet, ha időközben megváltozott
                    if (existingGame.DisplayName != newName)
                    {
                        existingGame.DisplayName = newName;
                    }
                }
                else
                {
                    // 3. HA NEM: Hozzáadjuk az új játékot a listához, friss időbélyeggel
                    FoundGames.Add(new DiscoveredGame
                    {
                        DisplayName = newName,
                        IpAddress = e.IpAddress,
                        LastSeen = DateTime.Now
                    });
                    Debug.WriteLine($"Új játék észlelve: {e.HostName} ({e.IpAddress})");
                }
                SetStatus($"Játék talált: {FoundGames.Count} elérhető host.", StatusType.Info);
            });
        }

        private async void NetworkService_NetworkErrorOccurred(object sender, string errorMessage)
        {
            // Ha épp navigálunk el, ne csináljunk semmit ===
            if (_isNavigatingAway)
            {
                Debug.WriteLine("MainViewModel.NetworkErrorOccurred: Figyelmen kívül hagyva (navigáció folyamatban).");
                return;
            }
            // A UI szálra váltunk a dialógushoz
            await DispatcherHelper.RunAsync(async () =>
            {
                // 1. Állapotok visszaállítása (a háttérben)
                IsJoiningGame = false;

                // 2. Felugró ablak megjelenítése
                // (A 'errorMessage' itt lehet technikai, vagy "Az ellenfél megszakította...")
                var dialog = new ContentDialog
                {
                    Title = "Hálózati Hiba",
                    Content = errorMessage,
                    PrimaryButtonText = "OK"
                };

                try
                {
                    await dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Hiba a NetworkError dialógus megjelenítésekor (MainVM): {ex.Message}");

                }
                // 3. A StatusMessage-t csak a dialógus UTÁN állítjuk be,
                //    hogy a felhasználó lássa a hiba okát.
                SetStatus(errorMessage, StatusType.Error);
            });
        }

        private void NetworkService_HostConnectionEstablished(object sender, EventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                _networkService.StopHosting();
                SetStatus($"Kliens csatlakozott. Válassz táblaméretet...", StatusType.Info);

                UnsubscribeFromNetworkEvents();

                var networkParams = new List<Parameter>
                {
                      new NamedParameter("isVsComputer", false),
                      new NamedParameter("isNetworkGame", true),
                      new NamedParameter("myPlayerName", this.PlayerName)
                };
                _viewService.OpenPage<GameSizeViewModel>(networkParams.ToArray());
            });
        }

        private void NetworkService_GameStarted(object sender, GameStartedEventArgs e)
        {
            if (!e.IsHost)
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    SetStatus($"Játék indul {e.OpponentName} ellen ({e.BoardSize}x{e.BoardSize})...", StatusType.Info);

                    UnsubscribeFromNetworkEvents();

                    var networkParams = new List<Parameter>
                    {
                          new NamedParameter("boardSizeParam", e.BoardSize),
                          new NamedParameter("isVsComputerParam", false),
                          new NamedParameter("isNetworkGameParam", true),
                          new NamedParameter("isHostParam", false),
                          new NamedParameter("myPlayerNameParam", this.PlayerName),
                          new NamedParameter("opponentNameParam", e.OpponentName)

                    };
                    _viewService.OpenPage<GameViewModel>(networkParams.ToArray());
                });
            }
        }

        /// <summary>
        /// A DispatcherTimer metódusa, ami másodpercenként lefut,
        /// és eltávolítja a listáról azokat a hostokat, amik 5s+ ideje nem adtak életjelet.
        /// </summary>
        private void CleanupStaleGames_Tick(object sender, object e)
        {
            // Keressük az összes "lejárt" játékot (ahol 5s-nél régebbi az utolsó életjel)
            var staleGames = FoundGames.Where(g => DateTime.Now - g.LastSeen > TimeSpan.FromSeconds(StaleGameTimeoutSeconds)).ToList(); // Külön listába mentjük, hogy ne a ciklus alatt módosítsuk a gyűjteményt

            // Eltávolítjuk őket
            if (staleGames.Any())
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    foreach (var game in staleGames)
                    {
                        FoundGames.Remove(game);
                        Debug.WriteLine($"Lejárt játék eltávolítva: {game.DisplayName} ({game.IpAddress})");
                    }

                    // Frissítjük a státuszüzenetet
                    if (FoundGames.Count == 0)
                    {
                        SetStatus("Játékok keresése aktív...", StatusType.Info);
                    }
                    else
                    {
                        SetStatus($"Játék talált: {FoundGames.Count} elérhető host.", StatusType.Info);
                    }
                });
            }
        }

        // ===================================================================
        // --- TAKARÍTÁS ---
        // ===================================================================

        private void StopAllNetworkActivity()
        {
            _networkService.StopHosting();
            _networkService.StopDiscovering();
            _networkService.Disconnect();

            IsHosting = false;
            IsJoiningGame = false;
            if (IsSearching) { StopDiscovery(); }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            Debug.WriteLine("MainViewModel: Leiratkozás a hálózati eseményekről...");
            _networkService.GameFound -= NetworkService_GameFound;
            _networkService.NetworkErrorOccurred -= NetworkService_NetworkErrorOccurred;
            _networkService.HostConnectionEstablished -= NetworkService_HostConnectionEstablished;
            _networkService.GameStarted -= NetworkService_GameStarted;
        }


        public override void Cleanup()
        {
            // === Időzítő leállítása ===
            if (_discoveryCleanupTimer != null)
            {
                _discoveryCleanupTimer.Stop();
                _discoveryCleanupTimer.Tick -= CleanupStaleGames_Tick;
                _discoveryCleanupTimer = null;
            }

            UnsubscribeFromNetworkEvents();

            StopAllNetworkActivity();

            base.Cleanup();
        }
    }
}