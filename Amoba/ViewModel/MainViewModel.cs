using Amoba.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autofac.Core; // Szükséges a NamedParameterhez
using Autofac;

namespace Amoba.ViewModel
{
    // DiscoveredGame osztály változatlan
    public class DiscoveredGame : ObservableObject
    {
        public string DisplayName { get; set; }
        public string IpAddress { get; set; }
    }

    public class MainViewModel : ViewModelBase
    {
        private readonly IViewService _viewService;
        private readonly INetworkService _networkService;

        private bool _isJoiningGame = false;
        private bool _isSearching = false;
        private string _statusMessage = string.Empty; // Átnevezve HostingStatusMessage-ről

        public ObservableCollection<DiscoveredGame> FoundGames { get; } = new ObservableCollection<DiscoveredGame>();

        public bool IsSearching
        {
            get => _isSearching;
            set => Set(ref _isSearching, value);
        }

        public string StatusMessage // Átnevezve HostingStatusMessage-ről
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
            _networkService.HostConnectionEstablished += NetworkService_HostConnectionEstablished;
            _networkService.GameStarted += NetworkService_GameStarted; // KLIENSNEK KELL!

            StartDiscovery(); // Automatikus keresés indítása
        }

        // ===================================================================
        // --- PARANCSOK ---
        // ===================================================================

        // 1. HELYI JÁTÉK INDÍTÁSA (Változatlan)
        private ICommand _startGame;
        public ICommand StartGame => _startGame ?? (_startGame = new RelayCommand(StartGameMethod));
        private void StartGameMethod()
        {
            StopDiscovery(); // Keresés leállítása
            _viewService.OpenPage<GameTypeViewModel>();
        }

        // 2. JÁTÉK HOSTOLÁSA (Változatlan)
        private ICommand _startHostingCommand;
        public ICommand StartHostingCommand => _startHostingCommand ?? (_startHostingCommand = new RelayCommand(ExecuteStartHosting));
        private async void ExecuteStartHosting()
        {
            StopDiscovery(); // Keresés leállítása
            string playerName = "Teszt Host";

            try
            {
                StatusMessage = "Játék hostolása folyamatban, TCP szerver indítása...";
                await _networkService.StartHostingAsync(playerName);
                await _networkService.StartAcceptingConnectionsAsync();
                StatusMessage = "Hostolás aktív! Várjuk a csatlakozást...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"HIBA: Nem sikerült hostolni. {ex.Message}";
                _networkService.StopHosting();
            }
        }

        // 3. JÁTÉK KERESÉSE (Változatlan)
        private ICommand _startDiscoveryCommand;
        public ICommand StartDiscoveryCommand => _startDiscoveryCommand ?? (_startDiscoveryCommand = new RelayCommand(StartDiscovery));
        private async void StartDiscovery()
        {
            if (IsSearching) return;
            _networkService.StopHosting(); // Hostolás leállítása, ha futott
            FoundGames.Clear();
            StatusMessage = "Játékok keresése aktív...";
            await _networkService.StartDiscoveringAsync();
            IsSearching = true;
        }

        // 4. KERESÉS LEÁLLÍTÁSA (Változatlan)
        private ICommand _stopDiscoveryCommand;
        public ICommand StopDiscoveryCommand => _stopDiscoveryCommand ?? (_stopDiscoveryCommand = new RelayCommand(StopDiscovery));
        private void StopDiscovery()
        {
            _networkService.StopDiscovering();
            IsSearching = false;
            StatusMessage = "Keresés leállítva.";
        }

        // 5. CSATLAKOZÁS A TALÁLT JÁTÉKHOZ (JAVÍTVA)
        private ICommand _joinGameCommand;
        public ICommand JoinGameCommand => _joinGameCommand ??
          (_joinGameCommand = new RelayCommand<DiscoveredGame>(ExecuteJoinGame, p => p != null && !_isJoiningGame));

        private async void ExecuteJoinGame(DiscoveredGame gameToJoin)
        {
            if (_isJoiningGame) return;
            _isJoiningGame = true;
            StopDiscovery(); // Keresés leállítása
            StatusMessage = $"Csatlakozás: {gameToJoin.IpAddress}...";

            try
            {
                // JAVÍTVA: A ConnectToGameAsync már nem ad vissza bool-t.
                await _networkService.ConnectToGameAsync(gameToJoin.IpAddress);
                // Ha sikeres, a StartReading elindul a NetworkService-ben és várja a START üzenetet.
                // A NetworkErrorOccurred esemény jelzi, ha hiba történt a kapcsolódáskor.
                StatusMessage = "Sikeresen csatlakozva. Várakozás a Hostra...";
            }
            catch (Exception ex) // Bár a NetworkService kezeli, itt is elkaphatjuk
            {
                StatusMessage = $"Csatlakozási hiba: {ex.Message}. Újraindítjuk a keresést.";
                StartDiscovery();
            }
            finally
            {
                _isJoiningGame = false;
            }
        }


        // ===================================================================
        // --- ESEMÉNYKEZELŐK ---
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
                // Ha csatlakozás közben jött a hiba, állítsuk vissza az _isJoiningGame-et
                _isJoiningGame = false;
                // Ha keresés közben, állítsuk le a keresést
                if (IsSearching) { StopDiscovery(); }
            });
        }

        // JAVÍTVA: Eseménykezelő a Host oldali navigációhoz
        private void NetworkService_HostConnectionEstablished(object sender, EventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                _networkService.StopHosting(); // Leállítjuk az UDP hirdetést
                StatusMessage = $"Kliens csatlakozott. Válassz táblaméretet...";

                var networkParams = new List<Parameter>
        {
          new NamedParameter("isVsComputer", false),
          new NamedParameter("isNetworkGame", true)
        };
                _viewService.OpenPage<GameSizeViewModel>(networkParams.ToArray());
            });
        }

        private void NetworkService_GameStarted(object sender, GameStartedEventArgs e)
        {
            if (!e.IsHost) // Ha én vagyok a Kliens
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    StatusMessage = $"Játék indul {e.OpponentName} ellen ({e.BoardSize}x{e.BoardSize})...";

                    // --- JAVÍTÁS ITT ---
                    var networkParams = new List<Parameter>
                {
                  new NamedParameter("boardSizeParam", e.BoardSize),
                  new NamedParameter("isVsComputerParam", false),
                  new NamedParameter("isNetworkGameParam", true),
                  new NamedParameter("isHostParam", false) // ÉN A KLIENS VAGYOK
                };
                    _viewService.OpenPage<GameViewModel>(networkParams.ToArray());
                });
            }
        }

        // ===================================================================
        // --- TAKARÍTÁS ---
        // ===================================================================

        public override void Cleanup()
        {
            // Leiratkozás az ÖSSZES eseményről (JAVÍTOTT NEVEK!)
            _networkService.GameFound -= NetworkService_GameFound;
            _networkService.NetworkErrorOccurred -= NetworkService_NetworkErrorOccurred;
            _networkService.HostConnectionEstablished -= NetworkService_HostConnectionEstablished;
            _networkService.GameStarted -= NetworkService_GameStarted; // KLIENSNEK KELL!

            _networkService.StopHosting();
            _networkService.StopDiscovering();
            _networkService.Disconnect(); // Biztosítjuk a TCP kapcsolat bontását is

            base.Cleanup();
        }
    }
}