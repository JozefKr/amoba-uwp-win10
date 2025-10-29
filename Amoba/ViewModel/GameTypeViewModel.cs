using Amoba.Model;
using Amoba.Services;
using Autofac;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Windows.Input;
using Windows.ApplicationModel.Core;

namespace Amoba.ViewModel
{
    public class GameTypeViewModel : ViewModelBase
    {
        private readonly IViewService _viewService;

        public GameTypeViewModel(IViewService viewService)
        {
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
        }

        private ICommand _isVsPlayer;
        public ICommand IsVsPlayer => _isVsPlayer ?? (_isVsPlayer = new RelayCommand(SelectPlayerMode));

        private ICommand _isVsComputer;
        public ICommand IsVsComputer => _isVsComputer ?? (_isVsComputer = new RelayCommand(SelectComputerMode));

        // Metódus a Játékos vs Játékos módhoz
        private void SelectPlayerMode()
        {
            // Navigálunk a GameSizeViewModel-re, jelezve, hogy NEM gép ellen játszunk
            _viewService.OpenPage<GameSizeViewModel>(
                new NamedParameter("isVsComputer", false),
                new NamedParameter("isNetworkGame", false) // <<<-- ÚJ: EXPLICITEN HAMIS
            );
        }

        // Metódus a Számítógép ellen módhoz
        private void SelectComputerMode()
        {
            // Navigálunk a GameSizeViewModel-re, jelezve, hogy gép ellen játszunk
            _viewService.OpenPage<GameSizeViewModel>(
                new NamedParameter("isVsComputer", true),
                new NamedParameter("isNetworkGame", false) // <<<-- ÚJ: EXPLICITEN HAMIS
            );
        }
    }
}
