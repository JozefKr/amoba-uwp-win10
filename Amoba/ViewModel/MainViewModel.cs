using Amoba.Services;
using Amoba.Views;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using System.Windows.Input;
using Windows.ApplicationModel.Core;

namespace Amoba.ViewModel
{
    // Megjegyzés: Az IViewService felel a GamePage-re navigálásért
    public class MainViewModel : ViewModelBase
    {
        private readonly IViewService viewService;

        public MainViewModel(IViewService viewService)
        {
            this.viewService = viewService;
        }

        private ICommand startGame;

        public ICommand StartGame
        {
            get
            {
                if (startGame == null)
                    startGame = new RelayCommand(StartGameMethod);
                return startGame;
            }
        }

        private void StartGameMethod()
        {
            viewService.OpenPage<GameTypeViewModel>();
        }
    }
}
