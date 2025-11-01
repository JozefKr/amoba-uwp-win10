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
                    RaisePropertyChanged(nameof(IsInMenu));
                    (JoinGameCommand as RelayCommand<DiscoveredGame>)?.RaiseCanExecuteChanged();
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

                    // Értesítjük a parancsokat ÉS a property-t
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

                    // --- JAVÍTÁS (1/3): Értesítjük a helyi gombokat is ---
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
            set => Set(ref _statusMessage, value);
        }

        /// <summary>
        /// Igaz, ha a hálózati gomboknak engedélyezve kell lenniük.
        /// </summary>
        public bool IsNetworkReady => !string.IsNullOrWhiteSpace(PlayerName);

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

            InitializeCommands();
        }

        // ===================================================================
        // --- Parancsok inicializálása ---
        // ===================================================================
        private void InitializeCommands()
        {
            StartLocalPvpCommand = new RelayCommand(StartLocalPvpMethod, () => IsInMenu);
            StartAiGameCommand = new RelayCommand(StartAiGameMethod, () => IsInMenu);

            // Hálózati parancsok
            StartHostingCommand = new RelayCommand(ExecuteStartHosting, () => IsNetworkReady);
            StartDiscoveryCommand = new RelayCommand(StartDiscovery, () => IsNetworkReady);
            StopDiscoveryCommand = new RelayCommand(StopDiscovery);
            JoinGameCommand = new RelayCommand<DiscoveredGame>(ExecuteJoinGame, p => p != null && !IsJoiningGame && IsNetworkReady);

            StopHostingCommand = new RelayCommand(ExecuteStopHosting);
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

        public ICommand StartLocalPvpCommand { get; private set; }
        public ICommand StartAiGameCommand { get; private set; }

        /// <summary>
        /// Indítja a Helyi (Player vs Player) játékot
        /// </summary>
        private void StartLocalPvpMethod()
        {
            StopAllNetworkActivity();

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

            var aiParams = new List<Parameter>
            {
                 new NamedParameter("isVsComputer", true),
                 new NamedParameter("isNetworkGame", false)
            };
            _viewService.OpenPage<GameSizeViewModel>(aiParams.ToArray());
        }

        #endregion

        #region Hálózati Játék (Host és Kliens)

        public ICommand StartHostingCommand { get; private set; }

        private async void ExecuteStartHosting()
        {
            StopDiscovery();
            IsHosting = true;

            try
            {
                StatusMessage = "Játék hostolása folyamatban, TCP szerver indítása...";
                await _networkService.StartHostingAsync(PlayerName);
                await _networkService.StartAcceptingConnectionsAsync();
                StatusMessage = "Hostolás aktív! Várjuk a csatlakozást...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"HIBA: Nem sikerült hostolni. {ex.Message}";
                _networkService.StopHosting();
                IsHosting = false;
            }
        }

        public ICommand StopHostingCommand { get; private set; }

        /// <summary>
        /// Leállítja a hostolást és visszaáll a menübe.
        /// </summary>
        private void ExecuteStopHosting()
        {
            _networkService.StopHosting();
            IsHosting = false;
            StatusMessage = "Hostolás leállítva.";
        }

        public ICommand StartDiscoveryCommand { get; private set; }

        private async void StartDiscovery()
        {
            if (IsSearching) return;
            _networkService.StopHosting();
            IsHosting = false;

            FoundGames.Clear();
            StatusMessage = "Játékok keresése aktív...";
            await _networkService.StartDiscoveringAsync();
            IsSearching = true;
        }

        public ICommand StopDiscoveryCommand { get; private set; }

        private void StopDiscovery()
        {
            _networkService.StopDiscovering();
            IsSearching = false;
            StatusMessage = "Keresés leállítva.";
        }

        public ICommand JoinGameCommand { get; private set; }

        private async void ExecuteJoinGame(DiscoveredGame gameToJoin)
        {
            if (IsJoiningGame) return;
            IsJoiningGame = true;
            StopDiscovery();
            StatusMessage = $"Csatlakozás: {gameToJoin.IpAddress}...";

            await _networkService.ConnectToGameAsync(gameToJoin.IpAddress, PlayerName);

            StatusMessage = "Sikeresen csatlakozva. Várakozás a Hostra...";
        }

        #endregion

        // ===================================================================
        // --- ESEMÉNYKEZELŐK (A NetworkService-től) ---
        // ===================================================================

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

        private void NetworkService_NetworkErrorOccurred(object sender, string errorMessage)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                StatusMessage = errorMessage;
                IsJoiningGame = false;
                if (IsSearching) { StopDiscovery(); }
            });
        }

        private void NetworkService_HostConnectionEstablished(object sender, EventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                _networkService.StopHosting();
                StatusMessage = $"Kliens csatlakozott. Válassz táblaméretet...";

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
                    StatusMessage = $"Játék indul {e.OpponentName} ellen ({e.BoardSize}x{e.BoardSize})...";

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