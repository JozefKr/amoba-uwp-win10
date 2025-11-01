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
    // Egyszerű osztály a talált játékok megjelenítéséhez a listában
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
                if (Set(ref _playerName, value))
                {
                    SavePlayerName(value);
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

        public bool IsNameEntryEnabled => !IsHosting && !IsSearching && !IsJoiningGame;

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        public bool IsNetworkReady => !string.IsNullOrWhiteSpace(PlayerName);

        // ===================================================================
        // --- PARANCS MEZŐK ---
        // ===================================================================
        private ICommand _startLocalPvpCommand;
        private ICommand _startAiGameCommand;

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
            _networkService.HostConnectionEstablished += NetworkService_HostConnectionEstablished;
            _networkService.GameStarted += NetworkService_GameStarted;

            InitializeCommands();

            //Oldalbetöltéskor egyből szerver keresés bekapcsol
            //StartDiscovery();
        }

        // ===================================================================
        // --- Parancsok inicializálása ---
        // ===================================================================
        private void InitializeCommands()
        {
            _startLocalPvpCommand = new RelayCommand(StartLocalPvpMethod);
            _startAiGameCommand = new RelayCommand(StartAiGameMethod);

            // Hálózati parancsok
            _startHostingCommand = new RelayCommand(ExecuteStartHosting, () => IsNetworkReady);
            _startDiscoveryCommand = new RelayCommand(StartDiscovery, () => IsNetworkReady);
            _stopDiscoveryCommand = new RelayCommand(StopDiscovery);
            _joinGameCommand = new RelayCommand<DiscoveredGame>(ExecuteJoinGame, p => p != null && !IsJoiningGame && IsNetworkReady);
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

        // JAVÍTÁS: A régi 'StartGame' property-t ez a kettő helyettesíti
        public ICommand StartLocalPvpCommand => _startLocalPvpCommand;
        public ICommand StartAiGameCommand => _startAiGameCommand;

        /// <summary>
        /// Indítja a Helyi (Player vs Player) játékot
        /// </summary>
        private void StartLocalPvpMethod()
        {
            StopAllNetworkActivity();

            // Közvetlenül a GameSizeViewModel-re navigálunk
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

            // Közvetlenül a GameSizeViewModel-re navigálunk
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
        public ICommand StartHostingCommand => _startHostingCommand;

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

        // 3. JÁTÉK KERESÉSE (KLIENS)
        public ICommand StartDiscoveryCommand => _startDiscoveryCommand;

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

        // 4. KERESÉS LEÁLLÍTÁSA
        public ICommand StopDiscoveryCommand => _stopDiscoveryCommand;

        private void StopDiscovery()
        {
            _networkService.StopDiscovering();
            IsSearching = false;
            StatusMessage = "Keresés leállítva.";
        }

        // 5. CSATLAKOZÁS A TALÁLT JÁTÉKHOZ (KLIENS)
        public ICommand JoinGameCommand => _joinGameCommand;

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