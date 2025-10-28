using GalaSoft.MvvmLight;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Amoba.ViewModel
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // 1. ViewModel beállítása (ez már megvolt)
            if (e.Parameter is ViewModelBase viewModel)
            {
                this.DataContext = viewModel;
            }
            base.OnNavigatedTo(e);

            // 2. Feliratkozás a rendszer-vissza gombra
            SystemNavigationManager.GetForCurrentView().BackRequested += MainPage_BackRequested;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // 3. Leiratkozás a rendszer-vissza gombról
            SystemNavigationManager.GetForCurrentView().BackRequested -= MainPage_BackRequested;
        }

        /// <summary>
        /// Ez fut le, ha a felhasználó a FŐOLDALON nyomja meg a vissza gombot.
        /// </summary>
        private void MainPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            // FONTOS KÜLÖNBSÉG:
            // A GamePage-en azt ellenőriztük, hogy TUD-E visszalépni.
            // Itt azt ellenőrizzük, hogy NEM TUD-E visszalépni.
            if (Frame.CanGoBack)
            {
                // Ez egy vészhelyzeti eset, ha valahogy mégis idejutnánk
                // úgy, hogy van hova visszamenni.
                e.Handled = true;
                Frame.GoBack();
            }
            else
            {
                // Ez a normál eset: a MainPage az alkalmazás gyökere.
                // A felhasználó ki akar lépni.
                e.Handled = true; // Jelezzük, hogy kezeltük...
                CoreApplication.Exit(); // ...és bezárjuk az alkalmazást.
            }
        }
    }
}
