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
            set => Set(ref _enabled, value);
        }
        // --- Változatlan tulajdonságok vége ---


        private void SelectSizeMethod(string size)
        {
            // 1. Ha már le van tiltva (dupla kattintás), ne csinálj semmit
            if (!Enabled) return;

            if (int.TryParse(size, out int boardSize) && boardSize > 0)
            {
                // 2. Tiltás a navigáció előtt
                Enabled = false;

                var sizeParam = new NamedParameter("boardSizeParam", boardSize);
                var modeParam = new NamedParameter("isVsComputerParam", _isVsComputerMode);
                var parameters = new Parameter[] { sizeParam, modeParam };

                _viewService.OpenPage<GameViewModel>(parameters);

                // 3. A navigáció után már mindegy, de ha a felhasználó
                // visszanavigál, a ViewModel konstruktora újra lefut
                // és visszaállítja 'Enabled = true'-ra. Ez a logika így jó.
            }
        }
    }
}