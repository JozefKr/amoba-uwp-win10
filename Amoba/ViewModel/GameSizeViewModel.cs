using Amoba.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Autofac.Core;
using Autofac;
using System.Diagnostics;
using GalaSoft.MvvmLight.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Amoba.ViewModel
{
    /// <summary>
    /// A ViewModel a tábla méretének kiválasztásához.
    /// </summary>
    public class GameSizeViewModel : ViewModelBase
    {
        private readonly IViewService _viewService;
        private readonly INetworkService _networkService; // Ez null lehet helyi/AI módban
        private bool _isVsComputerMode;
        private string _statusMessage = string.Empty;
        private readonly string _myPlayerName;
        private ICommand _cancelHostCommand;

        /// <summary>
        /// Igaz, ha mi (a Host) kezdeményeztük a megszakítást.
        /// </summary>
        private bool _isCancellingHost = false;

        /// <summary>
        /// Igaz, ha hálózati játékmódban (Host) vagyunk.
        /// A XAML ehhez köti a "Mégse" gomb láthatóságát.
        /// </summary>
        public bool IsNetworkGame { get; }


        /// <summary>
        /// Univerzális konstruktor, kezeli a Helyi, AI és Hálózati (Host) indítást.
        /// A DI konténer (Autofac) a NamedParameter-ek alapján tölti fel.
        /// </summary>
        public GameSizeViewModel(
            IViewService viewService,
            bool isVsComputer,
            bool isNetworkGame = false, // Alapértelmezett: false (helyi játék)
            INetworkService networkService = null, // Alapértelmezett: null
            string myPlayerName = null) // Alapértelmezett: null
        {
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            _networkService = networkService; // Ez null, ha helyi játék
            _isVsComputerMode = isVsComputer;
            IsNetworkGame = isNetworkGame; // A public property beállítása
            _myPlayerName = myPlayerName;
            Enabled = true;

            if (IsNetworkGame && _networkService != null)
            {
                _networkService.NetworkErrorOccurred += NetworkService_NetworkErrorOccurred;
                _networkService.OpponentDisconnected += NetworkService_OpponentDisconnected;
                StatusMessage = "Ellenfél csatlakozott! Válassz méretet a játék indításához:";
            }
            else
            {
                StatusMessage = "Válassz pályaméretet:";
            }
        }

        /// <summary>
        /// Parancs a hostolás leállítására és a főmenübe való visszatérésre.
        /// </summary>
        public ICommand CancelHostCommand => _cancelHostCommand ?? (_cancelHostCommand = new RelayCommand(ExecuteCancelHost));

        /// <summary>
        /// Leállítja a hálózati műveleteket (hostolás) és visszanavigál a főoldalra,
        /// TÖRÖLVE a navigációs előzményeket.
        /// </summary>
        private async void ExecuteCancelHost()
        {
            _isCancellingHost = true;

            // 1. Hálózati műveletek megszakítása
            if (IsNetworkGame && _networkService != null)
            {
                try
                {
                    // A központi megszakító metódus hívása.
                    await _networkService.CancelAllOperationsAsync();
                }
                catch (Exception ex)
                {
                    // Ha a megszakítás során hiba történik, azt naplózzuk.
                    Debug.WriteLine($"Hiba a hostolás megszakítása (CancelAllOperationsAsync) során: {ex.Message}");
                }
            }

            // 2. Navigáció a Főmenübe ÉS Előzmények Törlése
            //    (Ezt a logikát a GameViewModel-ből másoltuk át,
            //     hogy a "vissza" gomb ne működjön a MainPage-ről)
            await DispatcherHelper.RunAsync(() =>
            {
                try
                {
                    if (!(Window.Current.Content is Frame rootFrame))
                    {
                        // Vészhelyzeti eset: ha nincs Frame, a régi módon navigálunk
                        _viewService.OpenPage<MainViewModel>();
                        return;
                    }

                    // 1. Navigálunk a főoldalra
                    _viewService.OpenPage<MainViewModel>();

                    // 2. TÖRÖLJÜK A TELJES NAVIGÁCIÓS VERMET
                    //    Ez akadályozza meg, hogy a vissza gomb
                    //    visszadobja a felhasználót a GameSizePage-re.
                    rootFrame.BackStack.Clear();

                    Debug.WriteLine("ExecuteCancelHost: Navigáció Főoldalra sikeres, előzmények törölve.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FATALIS Hiba a navigáció közben (ExecuteCancelHost): {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Visszajelzés a felhasználónak a méretválasztás állapotáról vagy hibájáról.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => Set(ref _statusMessage, value);
        }

        private bool _enabled;
        /// <summary>
        /// Engedélyezi/tiltja a méretválasztó gombokat.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

        private ICommand _selectSize;
        /// <summary>
        /// Parancs a táblaméret kiválasztásához és a játék indításához.
        /// </summary>
        public ICommand SelectSize => _selectSize ?? (_selectSize = new RelayCommand<string>(SelectSizeMethod));

        /// <summary>
        /// Végrehajtja a méret kiválasztását. Hálózati módban elküldi a méretet, majd navigál.
        /// </summary>
        private async void SelectSizeMethod(string size)
        {
            if (!Enabled) return;

            if (int.TryParse(size, out int boardSize) && boardSize > 0)
            {
                Enabled = false;
                StatusMessage = "Feldolgozás...";

                try
                {
                    var gameParams = new List<Parameter>
                    {
                        new NamedParameter("boardSizeParam", boardSize),
                        new NamedParameter("isVsComputerParam", _isVsComputerMode),
                        new NamedParameter("isNetworkGameParam", IsNetworkGame), // A public property-t használjuk
                        new NamedParameter("myPlayerNameParam", _myPlayerName)
                    };

                    if (IsNetworkGame && _networkService != null)
                    {
                        // HÁLÓZATI: Host küld méretet
                        StatusMessage = "Méret küldése az ellenfélnek...";
                        gameParams.Add(new NamedParameter("isHostParam", true));
                        await _networkService.InitiateNetworkGameStartAsync(boardSize, _myPlayerName);
                    }
                    else
                    {
                        // HELYI/AI JÁTÉK
                        gameParams.Add(new NamedParameter("isHostParam", false));
                    }

                    string opponentName = _networkService?.CachedOpponentName;
                    if (!string.IsNullOrEmpty(opponentName))
                    {
                        gameParams.Add(new NamedParameter("opponentNameParam", opponentName));
                    }
                    else if (IsNetworkGame)
                    {
                        Debug.WriteLine("FIGYELEM (GameSizeVM): Az ellenfél neve (CachedOpponentName) null maradt a navigáció pillanatában.");
                    }

                    // NAVIGÁCIÓ (MINDIG LEFUT)
                    StatusMessage = "Játék indítása...";
                    _viewService.OpenPage<GameViewModel>(gameParams.ToArray());
                }
                catch (Exception ex)
                {
                    StatusMessage = $"HIBA: {ex.Message}";
                    Debug.WriteLine($"Hiba a méret kiválasztása/küldése során: {ex.Message}");
                    Enabled = true;
                }
            }
            else
            {
                StatusMessage = "Hiba: Érvénytelen méret.";
                Debug.WriteLine($"Érvénytelen méret paraméter: {size}");
                Enabled = true;
            }
        }

        /// <summary>
        /// Akkor fut le, ha a kliens váratlanul (pl. app bezárás) bontja a kapcsolatot.
        /// </summary>
        private void NetworkService_OpponentDisconnected(object sender, EventArgs e)
        {
            if (_isCancellingHost)
            {
                Debug.WriteLine("GameSizeVM: OpponentDisconnected fogadva, de figyelmen kívül hagyva (mi szakítottunk).");
                return;
            }

            Debug.WriteLine("GameSizeVM: OpponentDisconnected esemény fogadva.");
            // Ugyanazt a hibakezelőt hívjuk meg, mint a NetworkErrorOccurred,
            // de egy egyértelmű, felhasználóbarát üzenettel.
            NetworkService_NetworkErrorOccurred(sender, "Az ellenfél váratlanul bontotta a kapcsolatot.");
        }

        /// <summary>
        /// Akkor fut le, ha a NetworkService hibát vagy 'CANCEL_WAIT' üzenetet észlel.
        /// (Most már felugró ablakot mutat, és csak OK után navigál)
        /// </summary>
        private async void NetworkService_NetworkErrorOccurred(object sender, string errorMessage)
        {
            if (_isCancellingHost)
            {
                Debug.WriteLine("GameSizeVM: NetworkErrorOccurred fogadva, de figyelmen kívül hagyva (mi szakítottunk).");
                return;
            }

            // A UI szálra kell váltanunk a dialógus megjelenítéséhez
            await DispatcherHelper.RunAsync(async () =>
            {
                // 1. Minden hálózati tevékenység leállítása
                // (Ez lecseréli a régi StopHosting és Disconnect hívásokat)
                try
                {
                    if (_networkService != null)
                    {
                        await _networkService.CancelAllOperationsAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Hiba a CancelAllOperationsAsync hívásakor (NetworkErrorOccurred): {ex.Message}");
                }

                // 2. Felugró ablak megjelenítése a hibaüzenettel
                // (A 'errorMessage' itt tipikusan: "Az ellenfél megszakította...")
                var dialog = new ContentDialog
                {
                    Title = "Kapcsolat Megszakadt",
                    Content = errorMessage,
                    PrimaryButtonText = "OK (Főmenü)"
                };

                // 3. Várakozás, amíg a felhasználó le-OK-zza
                await dialog.ShowAsync();

                // 4. Navigáció a Főmenübe (az 'OK' megnyomása után)
                try
                {
                    if (!(Window.Current.Content is Frame rootFrame))
                    {
                        _viewService.OpenPage<MainViewModel>();
                        return;
                    }
                    _viewService.OpenPage<MainViewModel>();
                    rootFrame.BackStack.Clear();
                    Debug.WriteLine("NetworkErrorOccurred: Navigáció Főoldalra sikeres (dialógus után).");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FATALIS Hiba a navigáció közben (NetworkErrorOccurred): {ex.Message}");
                }
            });
        }

        public override void Cleanup()
        {
            if (IsNetworkGame && _networkService != null)
            {
                _networkService.NetworkErrorOccurred -= NetworkService_NetworkErrorOccurred;
                _networkService.OpponentDisconnected -= NetworkService_OpponentDisconnected;
            }
            base.Cleanup();
        }
    }
}