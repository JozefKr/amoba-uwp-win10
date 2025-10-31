using Amoba.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic; // Szükséges a List<Parameter>-hez
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autofac.Core; // Szükséges a NamedParameterhez
using Autofac;

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
        private bool _isSearching = false;
        private string _statusMessage = string.Empty;
        private string _playerName = "Játékos"; // Alapértelmezett név
        public string PlayerName
        {
            get => _playerName;
            set => Set(ref _playerName, value);
        }

        // --- Publikus Tulajdonságok a UI-hoz ---
        public ObservableCollection<DiscoveredGame> FoundGames { get; } = new ObservableCollection<DiscoveredGame>();

        public bool IsSearching
        {
            get => _isSearching;
            set => Set(ref _isSearching, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => Set(ref _statusMessage, value);
        }

        // ===================================================================
        // --- KONSTRUKTOR ---
        // ===================================================================

        public MainViewModel(IViewService viewService, INetworkService networkService)
        {
            _viewService = viewService;
            _networkService = networkService;

            // Feliratkozás a hálózati eseményekre
            _networkService.GameFound += NetworkService_GameFound;
            _networkService.NetworkErrorOccurred += NetworkService_NetworkErrorOccurred;
            _networkService.HostConnectionEstablished += NetworkService_HostConnectionEstablished; // Host navigál a GameSize-ra
            _networkService.GameStarted += NetworkService_GameStarted; // Kliens navigál a Game-re

            StartDiscovery(); // Automatikus keresés indítása a főoldal megnyitásakor
        }

        // ===================================================================
        // --- PARANCSOK ---
        // ===================================================================

        #region Helyi Játék

        // 1. HELYI JÁTÉK INDÍTÁSA
        private ICommand _startGame;
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
        private ICommand _startHostingCommand;
        public ICommand StartHostingCommand => _startHostingCommand ?? (_startHostingCommand = new RelayCommand(ExecuteStartHosting));

        private async void ExecuteStartHosting()
        {
            StopDiscovery(); // Leállítjuk a keresést (kölcsönös kizárás)

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
            }
        }

        // 3. JÁTÉK KERESÉSE (KLIENS)
        private ICommand _startDiscoveryCommand;
        public ICommand StartDiscoveryCommand => _startDiscoveryCommand ?? (_startDiscoveryCommand = new RelayCommand(StartDiscovery));

        private async void StartDiscovery()
        {
            if (IsSearching) return; // Ne indítsuk újra, ha már fut
            _networkService.StopHosting(); // Hostolás leállítása (kölcsönös kizárás)

            FoundGames.Clear();
            StatusMessage = "Játékok keresése aktív...";
            await _networkService.StartDiscoveringAsync();
            IsSearching = true; // Frissíti a UI-t, hogy a "Keresés Leállítása" gomb megjelenjen
        }

        // 4. KERESÉS LEÁLLÍTÁSA
        private ICommand _stopDiscoveryCommand;
        public ICommand StopDiscoveryCommand => _stopDiscoveryCommand ?? (_stopDiscoveryCommand = new RelayCommand(StopDiscovery));

        private void StopDiscovery()
        {
            _networkService.StopDiscovering();
            IsSearching = false; // JAVÍTVA: Helyesen frissíti a UI állapotot
            StatusMessage = "Keresés leállítva.";
        }

        // 5. CSATLAKOZÁS A TALÁLT JÁTÉKHOZ (KLIENS)
        private ICommand _joinGameCommand;
        public ICommand JoinGameCommand => _joinGameCommand ??
            (_joinGameCommand = new RelayCommand<DiscoveredGame>(ExecuteJoinGame, p => p != null && !_isJoiningGame));

        private async void ExecuteJoinGame(DiscoveredGame gameToJoin)
        {
            if (_isJoiningGame) return;
            _isJoiningGame = true;
            StopDiscovery();
            StatusMessage = $"Csatlakozás: {gameToJoin.IpAddress}...";

            // A NetworkService.ConnectToGameAsync kezeli a saját hibáit,
            // és a NetworkErrorOccurred eseményen keresztül jelez, ha baj van.
            await _networkService.ConnectToGameAsync(gameToJoin.IpAddress, PlayerName);

            // Ha sikeres, várunk a Hostra. Ha sikertelen, a NetworkErrorOccurred kezeli.
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
                _isJoiningGame = false; // Engedélyezzük az újbóli csatlakozást
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

                // Navigálás a GameSizePage-re, jelezve, hogy hálózati módban vagyunk
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

                    // Navigálás a GamePage-re a Host által küldött paraméterekkel
                    var networkParams = new List<Parameter>
                    {
                         new NamedParameter("boardSizeParam", e.BoardSize),
                         new NamedParameter("isVsComputerParam", false),
                         new NamedParameter("isNetworkGameParam", true),
                         new NamedParameter("isHostParam", false), // ÉN A KLIENS VAGYOK
                         // Átadjuk a Kliens nevét a GameViewModel-nek
                         new NamedParameter("myPlayerNameParam", this.PlayerName),
                         // Átadjuk a Host nevét (amit az eseményben kaptunk)
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