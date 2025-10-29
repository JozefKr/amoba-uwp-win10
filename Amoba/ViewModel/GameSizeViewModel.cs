using Amoba.Services;
using Autofac;
using Autofac.Core;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Windows.Input;

namespace Amoba.ViewModel
{
    public class GameSizeViewModel : ViewModelBase
    {
        private readonly IViewService _viewService;
        private readonly INetworkService _networkService; // ÚJ: Hálózati szolgáltatás
        private bool _isVsComputerMode;
        private bool _isNetworkGame; //Állapotjelző

        // Módosított konstruktor: Most már 3 paramétert fogad
        public GameSizeViewModel(IViewService viewService, INetworkService networkService, bool isVsComputer, bool isNetworkGame = false)
        {
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));

            _isVsComputerMode = isVsComputer;
            _isNetworkGame = isNetworkGame; // Rögzítjük, hogy hálózati módban vagyunk-e

            Enabled = true;
        }

        // ÚJ KONSTRUKTOR: EZT FOGJA HÍVNI AZ AUTOFAC A HELYI/AI JÁTÉKHOZ
        // (Ahol csak a két bool paraméter érkezik)
        public GameSizeViewModel(IViewService viewService, bool isVsComputer, bool isNetworkGame = false)
        {
            _viewService = viewService;
            _isVsComputerMode = isVsComputer;
            _isNetworkGame = isNetworkGame; // Ez most false
            _networkService = null; // Biztos, ami biztos: null-ra állítjuk
            Enabled = true;
        }

        // ... (Enabled property változatlan) ...
        private bool _enabled;
        public bool Enabled
        {
            get { return _enabled; }
            set => Set(ref _enabled, value);
        }

        // ... (SelectSize ICommand változatlan) ...
        private ICommand _selectSize;
        public ICommand SelectSize
        {
            get
            {
                if (_selectSize == null)
                    // Megj.: RelayCommand<string> helyett RelayCommand<int> hasznosabb lenne, 
                    // de maradunk a stringnél a kódkonzisztencia miatt.
                    _selectSize = new RelayCommand<string>(SelectSizeMethod);
                return _selectSize;
            }
        }

        private async void SelectSizeMethod(string size) // A metódus most már ASZINKRON
        {
            if (!Enabled) return;

            if (int.TryParse(size, out int boardSize) && boardSize > 0)
            {
                Enabled = false; // Tiltás a navigáció előtt

                // --- FONTOS LOGIKAI ELÁGAZÁS ---
                if (_isNetworkGame && _networkService != null)
                {
                    // 1. ESET: HÁLÓZATI JÁTÉK (HOST KIVÁLASZT)
                    // Küldjük el a méretet az ellenfélnek, majd navigálunk.
                    // A SendBoardSizeAsync a NetworkService-ben kell, hogy meghívja a START üzenetet is!
                    await _networkService.SendBoardSizeAsync(boardSize);
                }

                // 2. ESET: HELYI VAGY AI JÁTÉK (És navigálunk)
                // Ez az ág most már minden esetben lefut, de hálózati módban a fent küldött méret is itt továbbítódik.

                var sizeParam = new NamedParameter("boardSizeParam", boardSize);
                var modeParam = new NamedParameter("isVsComputerParam", _isVsComputerMode);
                var parameters = new Parameter[] { sizeParam, modeParam };

                _viewService.OpenPage<GameViewModel>(parameters);
                // A GameSizePage automatikusan visszaállítja az Enabled=true állapotot, ha visszanavigálunk.
            }
        }
    }
}