using Amoba.Services;
using Amoba.Views;
using Autofac;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using System.Windows.Input;
using Windows.ApplicationModel.Core;

namespace Amoba.ViewModel
{
    // Megjegyzés: Az IViewService-nek tartalmaznia kell az OpenDialog<T> metódust
    public class MainViewModel : ViewModelBase
    {
        private readonly IViewService viewService;

        // A WPF-es IDialogCoordinator/MahApps.Metro helyett a natív ViewService-t használjuk
        // A GameSizeDialog-nak ContentDialog-ból kell származnia az UWP-ben.
        private readonly GameSizeDialog dialog;

        // A IDialogCoordinator eltávolítva a konstruktorból
        public MainViewModel(IViewService viewService)
        {
            this.viewService = viewService;

            // A dialog példányosítása: Mivel a ViewService kezeli a DIALOG megjelenítést, 
            // A dialog példányosítása: Mivel a ViewService kezeli a DIALOG megjelenítést, 
            // a MainViewModel-nek csak a GameSizeViewModel-t kell inicializálnia.
            // A ContentDialog-ot (GameSizeDialog) a ViewService fogja példányosítani az OpenDialog hívásakor.
            // Hagyjuk el a helyi GameSizeDialog inicializálást itt:
            // dialog = new GameSizeDialog(); 
            // dialog.DataContext = new GameSizeViewModel(); 

            // Ha a GameSizeViewModel-en keresztül indítjuk a Messenger üzenetét, 
            // akkor elegendő a regisztráció.
            Messenger.Default.Register<int>(this, SizeChecked);
        }

        private ICommand startGame;

        public ICommand StartGame
        {
            get
            {
                if (startGame == null)
                    // A CommandParameter<Window> helyett RelayCommand-ot használunk, és a logikát a ViewService kezeli.
                    startGame = new RelayCommand(StartGameMethod);
                return startGame;
            }
        }

        private ICommand exitApp;

        public ICommand ExitApp
        {
            get
            {
                if (exitApp == null)
                    // Az Environment.Exit() helyett a CoreApplication.Exit() a helyes UWP kilépési metódus
                    exitApp = new RelayCommand(ExitAppMethod);
                return exitApp;
            }
        }

        // Új metódus az UWP kilépéshez
        private void ExitAppMethod()
        {
            // A CoreApplication.Exit() szigorúan kilépteti az UWP alkalmazást
            CoreApplication.Exit();
        }

        // Az IDialogCoordinator használata helyett az OpenDialog hívása a ViewService-en keresztül
        private void StartGameMethod()
        {
            // Feltételezve, hogy a GameSizeDialog egy ContentDialog-ból származik
            // és a ViewService tudja, hogy a GameSizeViewModel a hozzá tartozó ViewModel.
            // Ehelyett a ViewService.OpenDialog-ot hívjuk.
            viewService.OpenDialog<GameSizeViewModel>();

            // Megjegyzés: Ha a GameSizeDialog megnyitása előtt DataContext-et kell beállítani, 
            // azt a ViewService-ben kell megtenni, a ViewModel feloldása után.
        }

        private void SizeChecked(int gameSize)
        {
            // Nincs szükség a coordinator.HideMetroDialogAsync hívására, 
            // mert a ContentDialog-ot a GameSizeViewModel maga zárja be (pl. Dialog.Hide() hívással 
            // vagy Button Click eseménnyel), mielőtt elküldi az üzenetet.

            // Navigáció a fő játék nézetre a kiválasztott mérettel
            viewService.OpenPage<GameViewModel>(new NamedParameter("gameSize", gameSize));

            // Ha ContentDialog-ot használtál, a GameSizeViewModel-ből érkező üzenet 
            // jelzi, hogy a párbeszédablakot a ViewService-nek kell bezárnia. 
            // Mivel ez a kód a navigációra fókuszál, feltételezzük, hogy a dialogus már bezárult.
        }
    }
}
