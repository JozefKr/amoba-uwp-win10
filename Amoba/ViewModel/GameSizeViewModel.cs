using Amoba.Services;
using Autofac;
using Autofac.Core; // Szükséges a Parameter és NamedParameter típusokhoz
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Windows.Input;

namespace Amoba.ViewModel
{
    public class GameSizeViewModel : ViewModelBase
    {
        private readonly IViewService _viewService;
        private bool _isVsComputerMode; // Új mező a játékmód tárolására

        // Módosított konstruktor: Megkapja a viewService-t ÉS az isVsComputer paramétert
        public GameSizeViewModel(IViewService viewService, bool isVsComputer)
        {
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            _isVsComputerMode = isVsComputer; // Eltároljuk a kapott játékmódot
            Enabled = true;
        }

        // --- A többi tulajdonság (Enabled, SelectSize) változatlan ---
        private ICommand _selectSize;
        public ICommand SelectSize
        {
            get
            {
                if (_selectSize == null)
                    _selectSize = new RelayCommand<string>(SelectSizeMethod);
                return _selectSize;
            }
        }

        private bool _enabled;
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                RaisePropertyChanged();
            }
        }
        // --- Változatlan tulajdonságok vége ---


        private void SelectSizeMethod(string size)
        {
            if (int.TryParse(size, out int boardSize) && boardSize > 0)
            {
                // Létrehozzuk a két NamedParameter objektumot a helyes nevekkel
                var sizeParam = new NamedParameter("boardSizeParam", boardSize);
                var modeParam = new NamedParameter("isVsComputerParam", _isVsComputerMode);

                // Explicit Parameter tömb létrehozása
                var parameters = new Parameter[] { sizeParam, modeParam };

                // Közvetlenül az IViewService OpenPage(params Parameter[] parameters) metódusát hívjuk meg
                _viewService.OpenPage<GameViewModel>(parameters);
            }
        }
    }
}