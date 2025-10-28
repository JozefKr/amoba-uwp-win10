using GalaSoft.MvvmLight;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Amoba.ViewModel
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GameTypePage : Page
    {
        public GameTypePage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Amikor az oldalra navigálunk: ViewModel beállítása + Vissza-gomb feliratkozás.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // 1. ViewModel beállítása (ugyanaz a minta, mint a MainPage-en)
            if (e.Parameter is ViewModelBase viewModel)
            {
                this.DataContext = viewModel;
            }
            base.OnNavigatedTo(e);

            // 2. Feliratkozás a rendszer-vissza gombra
            SystemNavigationManager.GetForCurrentView().BackRequested += GameTypePage_BackRequested;
        }

        /// <summary>
        /// Amikor elhagyjuk az oldalt: Leiratkozás.
        /// </summary>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // 3. Leiratkozás a rendszer-vissza gombról
            SystemNavigationManager.GetForCurrentView().BackRequested -= GameTypePage_BackRequested;
        }

        /// <summary>
        /// Ez fut le, ha a JÁTÉKMÓD oldalon nyomják meg a vissza gombot.
        /// </summary>
        private void GameTypePage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            // Ezen az oldalon a "vissza" mindig a Főoldalt jelenti.
            if (Frame.CanGoBack)
            {
                // 4. Jelezzük, hogy kezeltük az eseményt
                e.Handled = true;

                // 5. Visszalépünk (a Főoldalra)
                Frame.GoBack();
            }
            // Nincs "else" ág, ami kilépne! A kilépés a Főoldal dolga.
        }
    }
}
