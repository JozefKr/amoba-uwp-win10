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

namespace Amoba.ViewModel
{
    public class DiscoveredGame : ObservableObject
    {
        public string DisplayName { get; set; }
        public string IpAddress { get; set; }
    }

    public class MainViewModel : ViewModelBase
    {
        private readonly IViewService _viewService;
        private readonly INetworkService _networkService;

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
                    // Értesítjük a parancsot, hogy a CanExecute állapota változott
                    (JoinGameCommand as RelayCommand<DiscoveredGame>)?.RaiseCanExecuteChanged();
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

                    // Értesítjük a parancsokat, hogy a CanExecute
                    // feltételük (IsNetworkReady) megváltozott.
                    (StartHostingCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StartDiscoveryCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (JoinGameCommand as RelayCommand<DiscoveredGame>)?.RaiseCanExecuteChanged();

                    RaisePropertyChanged(nameof(IsNetworkReady));
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
                }
            }
        }

        /// <summary>
        /// Igaz, ha a név-beviteli mezőnek engedélyezve kell lennie.
        /// (Nincs hálózati művelet folyamatban).
        /// </summary>
        public bool IsNameEntryEnabled => !IsHosting && !IsSearching && !IsJoiningGame;

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        /// <summary>
        /// Igaz, ha a hálózati gomboknak engedélyezve kell lenniük.
        /// (Van beírt név).
        /// </summary>
        public bool IsNetworkReady => !string.IsNullOrWhiteSpace(PlayerName);

        // ===================================================================
        // --- PARANCS MEZŐK ---
        // ===================================================================
        private ICommand _startGame;
        private ICommand _startHostingCommand;
        private ICommand _startDiscoveryCommand;
        private ICommand _stopDiscoveryCommand;
        private ICommand _joinGameCommand;


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
            _networkService.HostConnectionEstablished += NetworkService_HostConnectionEstablished; // Host navigál a GameSize-ra
            _networkService.GameStarted += NetworkService_GameStarted; // Kliens navigál a Game-re
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

        // 1. HELYI JÁTÉK INDÍTÁSA
        public ICommand StartGame => _startGame ?? (_startGame = new RelayCommand(StartGameMethod));

        private void StartGameMethod()
        {
            // Mielőtt elnavigálunk, állítsunk le minden hálózati tevékenységet
            StopAllNetworkActivity();
            _viewService.OpenPage<GameTypeViewModel>();
        }

        #endregion

        #region Hálózati Játék (Host és Kliens)

        // 2. JÁTÉK HOSTOLÁSA (SZERVER)
        public ICommand StartHostingCommand => _startHostingCommand ??
            (_startHostingCommand = new RelayCommand(ExecuteStartHosting,
                // Csak akkor hostolhat, ha van neve.
                () => IsNetworkReady));

        private async void ExecuteStartHosting()
        {
            StopDiscovery(); // Leállítjuk a keresést (kölcsönös kizárás)
            IsHosting = true;

            try
            {
                StatusMessage = "Játék hostolása folyamatban, TCP szerver indítása...";
                await _networkService.StartHostingAsync(PlayerName); // UDP Hirdetés indítása
                await _networkService.StartAcceptingConnectionsAsync(); // TCP Listener indítása
                StatusMessage = "Hostolás aktív! Várjuk a csatlakozást...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"HIBA: Nem sikerült hostolni. {ex.Message}";
                _networkService.StopHosting(); // Hiba esetén takarítunk
                IsHosting = false;
            }
        }

        // 3. JÁTÉK KERESÉSE (KLIENS)
        public ICommand StartDiscoveryCommand => _startDiscoveryCommand ??
            (_startDiscoveryCommand = new RelayCommand(StartDiscovery, () => IsNetworkReady));

        private async void StartDiscovery()
        {
            if (IsSearching) return; // Ne indítsuk újra, ha már fut
            _networkService.StopHosting(); // Hostolás leállítása (kölcsönös kizárás)
            IsHosting = false; // A keresés megszakítja a hostolást

            FoundGames.Clear();
            StatusMessage = "Játékok keresése aktív...";
            await _networkService.StartDiscoveringAsync();
            IsSearching = true;
        }

        // 4. KERESÉS LEÁLLÍTÁSA
 
        public ICommand StopDiscoveryCommand => _stopDiscoveryCommand ??
            (_stopDiscoveryCommand = new RelayCommand(StopDiscovery));

        private void StopDiscovery()
        {
            _networkService.StopDiscovering();
            IsSearching = false;
            StatusMessage = "Keresés leállítva.";
        }

        // 5. CSATLAKOZÁS A TALÁLT JÁTÉKHOZ (KLIENS)
        public ICommand JoinGameCommand => _joinGameCommand ??
            (_joinGameCommand = new RelayCommand<DiscoveredGame>(ExecuteJoinGame,
                p => p != null && !IsJoiningGame && IsNetworkReady));

        private async void ExecuteJoinGame(DiscoveredGame gameToJoin)
        {
            if (IsJoiningGame) return;
            IsJoiningGame = true; // Most már a property-t állítja be
            StopDiscovery();
            StatusMessage = $"Csatlakozás: {gameToJoin.IpAddress}...";

            await _networkService.ConnectToGameAsync(gameToJoin.IpAddress, PlayerName);

            StatusMessage = "Sikeresen csatlakozva. Várakozás a Hostra...";
        }

        #endregion

        // ===================================================================
        // --- ESEMÉNYKEZELŐK (A NetworkService-től) ---
        // ===================================================================

        // Lefut, ha a Kliens talál egy Hostot
        private void NetworkService_GameFound(object sender, GameFoundEventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                if (!FoundGames.Any(g => g.IpAddress == e.IpAddress))
                {
                    FoundGames.Add(new DiscoveredGame { DisplayName = $"{e.HostName} játéka", IpAddress = e.IpAddress });
                    StatusMessage = $"Játék talált: {FoundGames.Count} elérhető host.";
                }
            });
        }

        // Lefut, ha bármilyen hálózati művelet (Keresés, Csatlakozás) hibát dob
        private void NetworkService_NetworkErrorOccurred(object sender, string errorMessage)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                StatusMessage = errorMessage;
                IsJoiningGame = false; // Engedélyezzük az újbóli csatlakozást
                if (IsSearching) { StopDiscovery(); }
            });
        }

        // Lefut a HOST oldalon, amikor egy Kliens sikeresen csatlakozott
        private void NetworkService_HostConnectionEstablished(object sender, EventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // Már nem kell hirdetni, a Kliens megvan
                _networkService.StopHosting();
                StatusMessage = $"Kliens csatlakozott. Válassz táblaméretet...";

                // Navigálás a GameSizePage-re
                var networkParams = new List<Parameter>
                {
                     new NamedParameter("isVsComputer", false),
                     new NamedParameter("isNetworkGame", true),
                     new NamedParameter("myPlayerName", this.PlayerName)
                };
                _viewService.OpenPage<GameSizeViewModel>(networkParams.ToArray());
            });
        }

        // Lefut a KLIENS oldalon, amikor a Host elküldte a START üzenetet
        private void NetworkService_GameStarted(object sender, GameStartedEventArgs e)
        {
            if (!e.IsHost) // Csak a Kliens reagál erre a navigációhoz
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    StatusMessage = $"Játék indul {e.OpponentName} ellen ({e.BoardSize}x{e.BoardSize})...";

                    // Navigálás a GamePage-re
                    var networkParams = new List<Parameter>
                    {
                         new NamedParameter("boardSizeParam", e.BoardSize),
                         new NamedParameter("isVsComputerParam", false),
                         new NamedParameter("isNetworkGameParam", true),
                         new NamedParameter("isHostParam", false), // ÉN A KLIENS VAGYOK
                         new NamedParameter("myPlayerNameParam", this.PlayerName),
                         new NamedParameter("opponentNameParam", e.OpponentName)
                    };
                    _viewService.OpenPage<GameViewModel>(networkParams.ToArray());
                });
            }
        }

        // ===================================================================
        // --- TAKARÍTÁS ---
        // ===================================================================

        // Segédmetódus a hálózati műveletek leállítására
        private void StopAllNetworkActivity()
        {
            _networkService.StopHosting();
            _networkService.StopDiscovering();
            _networkService.Disconnect();

            IsHosting = false;
            IsJoiningGame = false;
            if (IsSearching) { StopDiscovery(); } // Ez már beállítja az IsSearching-et false-ra
        }

        public override void Cleanup()
        {
            // Leiratkozás az összes eseményről
            _networkService.GameFound -= NetworkService_GameFound;
            _networkService.NetworkErrorOccurred -= NetworkService_NetworkErrorOccurred;
            _networkService.HostConnectionEstablished -= NetworkService_HostConnectionEstablished;
            _networkService.GameStarted -= NetworkService_GameStarted;

            StopAllNetworkActivity();

            base.Cleanup();
        }
    }
}