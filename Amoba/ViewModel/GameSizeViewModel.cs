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
                // Ez a kijelölt sorod, most már helyesen fog lefutni
                _networkService.HostConnectionEstablished += NetworkService_HostConnectionEstablished;
                _networkService.NetworkErrorOccurred += NetworkService_NetworkErrorOccurred;
                StatusMessage = "Várakozás a Kliens csatlakozására...";
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
        /// Akkor fut le, amikor a Kliens sikeresen csatlakozott a TCP listenerhez.
        /// </summary>
        private void NetworkService_HostConnectionEstablished(object sender, EventArgs e)
        {
            // Amikor a kliens csatlakozik, frissítjük az üzenetet
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                StatusMessage = "Ellenfél csatlakozott! Válassz méretet a játék indításához:";
            });
        }

        /// <summary>
        /// Akkor fut le, ha a NetworkService hibát vagy 'CANCEL_WAIT' üzenetet észlel.
        /// </summary>
        private void NetworkService_NetworkErrorOccurred(object sender, string errorMessage)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                // 1. Megjelenítjük a hibaüzenetet (ami a CANCEL_WAIT esetén: "Az ellenfél megszakította...")
                StatusMessage = errorMessage;

                // 2. Leállítjuk a TCP listener-t
                _networkService.StopHosting();

                _networkService.Disconnect();

                // 3. Visszanavigálunk a MainViewModel-re
                Task.Delay(1500).ContinueWith((t) =>
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        _viewService.OpenPage<MainViewModel>();
                    });
                });
            });
        }

        public override void Cleanup()
        {
            if (IsNetworkGame && _networkService != null)
            {
                _networkService.HostConnectionEstablished -= NetworkService_HostConnectionEstablished;
                _networkService.NetworkErrorOccurred -= NetworkService_NetworkErrorOccurred;
            }
            base.Cleanup();
        }
    }
}