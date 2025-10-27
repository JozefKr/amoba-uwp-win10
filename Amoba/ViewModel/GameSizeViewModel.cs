using Amoba.Services;
using Autofac;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Windows.Input;

namespace Amoba.ViewModel
{
    public class GameSizeViewModel : ViewModelBase
    {
        private readonly IViewService _viewService;

        // A DI-n keresztül megkapja a navigációs szolgáltatást.
        public GameSizeViewModel(IViewService viewService)
        {
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            Enabled = true;
        }

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

        private void SelectSizeMethod(string size)
        {
            if (int.TryParse(size, out int boardSize) && boardSize > 0)
            {
                // A méret kiválasztása megtörtént.
                // Most azonnal navigálunk a GameViewModel-re (GamePage).

                // Mivel a GameViewModel egy pozicionális paramétert (int gameSize) vár,
                // a TypedParameter a legmegfelelőbb megoldás a DI-ben.
                var param = new TypedParameter(typeof(int), boardSize);

                // A ViewService.OpenPage feloldása mostantól a TypedParameter-t kapja meg.
                // A paramétert egy tömbbe csomagoljuk, hogy a ViewService a megfelelő túlterhelést válassza.
                _viewService.OpenPage<GameViewModel>(new NamedParameter("boardSizeParam", boardSize));
            }
        }
    }
}
