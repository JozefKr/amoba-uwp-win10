using Amoba.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Autofac.Core; // Szükséges a NamedParameterhez

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

        // --- PRIVÁT MEZŐK ---
        private bool _isJoiningGame = false;
        private bool _isSearching = false; // ÚJ MEZŐ
        private string _hostingStatusMessage = string.Empty;

        // --- PUBLIKUS PROPERTY-K ---
        public ObservableCollection<DiscoveredGame> FoundGames { get; } = new ObservableCollection<DiscoveredGame>();

        public bool IsSearching
        {
            get => _isSearching;
            set => Set(ref _isSearching, value);
        }

        public string HostingStatusMessage
        {
            get => _hostingStatusMessage;
            set => Set(ref _hostingStatusMessage, value);
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
            _networkService.HostGameReady += NetworkService_HostGameReady;
            _networkService.BoardSizeReceived += NetworkService_BoardSizeReceived;

            // Automatikus keresés indítása a ViewModel létrehozásakor
            // Ez a leglogikusabb a MainViewModel-ben
            StartDiscovery();
        }

        // ===================================================================
        // --- PARANCSOK ---
        // ===================================================================

        // 1. HELYI JÁTÉK INDÍTÁSA
        private ICommand _startGame;
        public ICommand StartGame => _startGame ?? (_startGame = new RelayCommand(StartGameMethod));
        private void StartGameMethod()
        {
            _networkService.StopDiscovering();
            _viewService.OpenPage<GameTypeViewModel>();
        }

        // 2. JÁTÉK HOSTOLÁSA (SZERVER)
        private ICommand _startHostingCommand;
        public ICommand StartHostingCommand => _startHostingCommand ?? (_startHostingCommand = new RelayCommand(ExecuteStartHosting));
        private async void ExecuteStartHosting()
        {
            // KÖLCSÖNÖS KIZÁRÁS: Leállítjuk a keresést, ha elindítjuk a Hostolást.
            _networkService.StopDiscovering();
            IsSearching = false; // Frissítjük a UI-t, hogy eltűnjön a 'Leállítás' gomb

            string playerName = "Teszt Host";

            try
            {
                HostingStatusMessage = "Játék hostolása folyamatban, TCP szerver indítása...";

                // UDP Hirdetés
                await _networkService.StartHostingAsync(playerName);

                // TCP Listener
                await _networkService.StartAcceptingConnectionsAsync();

                // SIKER Visszajelzés
                HostingStatusMessage = "Hostolás aktív! Várjuk a csatlakozást...";

            }
            catch (Exception ex)
            {
                HostingStatusMessage = $"HIBA: Nem sikerült hostolni. {ex.Message}";
                _networkService.StopHosting();
            }
        }

        // 3. JÁTÉK KERESÉSE (KLIENS)
        private ICommand _startDiscoveryCommand;
        public ICommand StartDiscoveryCommand => _startDiscoveryCommand ?? (_startDiscoveryCommand = new RelayCommand(StartDiscovery));
        private async void StartDiscovery()
        {
            if (IsSearching) return;

            // KÖLCSÖNÖS KIZÁRÁS: Leállítjuk a Hostolást, ha elindítjuk a keresést.
            _networkService.StopHosting();

            FoundGames.Clear();
            HostingStatusMessage = "Játékok keresése aktív...";

            await _networkService.StartDiscoveringAsync();
            IsSearching = true; // Állapot beállítása a UI frissítéséhez
        }

        // 4. KERESÉS LEÁLLÍTÁSA
        private ICommand _stopDiscoveryCommand;
        public ICommand StopDiscoveryCommand => _stopDiscoveryCommand ?? (_stopDiscoveryCommand = new RelayCommand(StopDiscovery));

        private void StopDiscovery()
        {
            _networkService.StopDiscovering();
            IsSearching = false;
            HostingStatusMessage = "Keresés leállítva.";
        }

        // 5. CSATLAKOZÁS A TALÁLT JÁTÉKHOZ
        private ICommand _joinGameCommand;
        public ICommand JoinGameCommand => _joinGameCommand ??
            (_joinGameCommand = new RelayCommand<DiscoveredGame>(ExecuteJoinGame, p => p != null && !_isJoiningGame));

        private async void ExecuteJoinGame(DiscoveredGame gameToJoin)
        {
            if (_isJoiningGame) return;

            _isJoiningGame = true;
            _networkService.StopDiscovering();
            HostingStatusMessage = $"Csatlakozás indítása: {gameToJoin.IpAddress}";

            try
            {
                bool success = await _networkService.ConnectToGameAsync(gameToJoin.IpAddress);

                if (success)
                {
                    // Ha a TCP kapcsolat sikeres, elnavigálunk a GamePage-re.
                    // A GameViewModel a Host START üzenetére várva fogja beállítani a játékot.

                    var networkParams = new List<Parameter>
                    {
                         new Autofac.NamedParameter("boardSizeParam", 3), // Átmeneti méret
                         new Autofac.NamedParameter("isVsComputerParam", false) // Jelzi, hogy nem AI ellen
                    };

                    _viewService.OpenPage<GameViewModel>(networkParams.ToArray());
                }
                else
                {
                    HostingStatusMessage = "Csatlakozás sikertelen. Újraindítjuk a keresést.";
                    StartDiscovery(); // Sikertelen csatlakozás után próbáljuk újra a keresést
                }
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
            // UI szálra kell visszatérni!
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // Előfordulhat, hogy a Host beállítása miatt itt hirdet is magáról, 
                // ezért a saját IP-t ki kell szűrnünk (ezt a NetworkService már megtette).

                // IP cím alapján ellenőrizzük, hogy nincs-e már a listában
                if (!FoundGames.Any(g => g.IpAddress == e.IpAddress))
                {
                    FoundGames.Add(new DiscoveredGame { DisplayName = $"{e.HostName} játéka", IpAddress = e.IpAddress });

                    // Frissítsük az állapotüzenetet a listázás megerősítésére
                    HostingStatusMessage = $"Játék talált: {FoundGames.Count} elérhető host.";
                }
            });
        }

        private void NetworkService_NetworkErrorOccurred(object sender, string errorMessage)
        {
            // Hiba esetén a UI szálon állítjuk be az üzenetet.
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // Ha hálózati hiba (pl. port foglalt), kiírjuk.
                HostingStatusMessage = errorMessage;
            });
        }

        private void NetworkService_HostGameReady(object sender, GameStartedEventArgs e)
        {
            // A Host MainViewModel-je megkapja a jelet, hogy a kliens csatlakozott.
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                _networkService.StopHosting();
                // Állapot: A Kliens várakozik, a Host választ
                HostingStatusMessage = $"Csatlakozva: {e.OpponentName}. VÁLASSZ TÁBLAMÉRETET...";

                // Navigáció a GameSizeViewModel-re
                // Átadjuk a NetworkService-t, hogy a GameSizeViewModel tudja elküldeni a méretet
                var parameters = new List<Parameter>
                {
                    // Az Autofac DI kezeli a networkService injektálást.
                    // Ha a GameSizeViewModel konstruktora kéri az INetworkService-t,
                    // a DI megoldja.
                };

                // Host navigál a GameSizePage-re
                _viewService.OpenPage<GameSizeViewModel>(); // Feltételezve, hogy a DI injektálja a NetworkService-t

                // A Kliens addig vár a BoardSizeReceived eseményre.
            });
        }

        // ÚJ ESEMÉNYKEZELŐ A KLIENS OLDALON
        private void NetworkService_BoardSizeReceived(object sender, int boardSize)
        {
            // A Kliens megkapta a méretet, most navigálhatunk!
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // 1. Állapotfrissítés:
                IsSearching = false;
                HostingStatusMessage = $"Tábla méret elfogadva ({boardSize}x{boardSize}). Játék indul...";

                // 2. NAVIGÁCIÓ: Elindítjuk a GameViewModel-t
                // A GameViewModel-ben a START üzenet (amit a Host a SIZE után küld) fejezi be a beállítást.
                var networkParams = new List<Autofac.Core.Parameter>
        {
             new Autofac.NamedParameter("boardSizeParam", boardSize),
             new Autofac.NamedParameter("isVsComputerParam", false)
        };

                // Elnavigálunk a GamePage-re
                _viewService.OpenPage<GameViewModel>(networkParams.ToArray());

                // Állapotüzenet takarítása (opcionális)
                HostingStatusMessage = string.Empty;
            });
        }


        // ===================================================================
        // --- TAKARÍTÁS ---
        // ===================================================================

        public override void Cleanup()
        {
            // Leiratkozás a memóriaszivárgás elkerülése érdekében
            _networkService.GameFound -= NetworkService_GameFound;
            _networkService.NetworkErrorOccurred -= NetworkService_NetworkErrorOccurred;

            // Biztosítjuk, hogy ne maradjon nyitott socket
            _networkService.StopHosting();
            _networkService.StopDiscovering();

            base.Cleanup();
        }
    }
}