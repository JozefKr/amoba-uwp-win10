// Ezekre a "using"-okra lesz szükséged a navigációhoz és a vissza-gombhoz
using Windows.UI.Core; // A SystemNavigationManager-hez (vissza-gomb)
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using GalaSoft.MvvmLight; // A ViewModelBase típus ellenőrzéséhez

namespace Amoba.Views
{
    /// <summary>
    /// Maga a GamePage "code-behind" fájlja.
    /// Ennek a fájlnak a felelőssége a Nézettel kapcsolatos
    /// események kezelése, mint a navigáció.
    /// </summary>
    public sealed partial class GamePage : Page
    {
        public GamePage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Akkor hívódik meg, amikor erre az oldalra navigálunk.
        /// Itt állítjuk be a ViewModel-t ÉS itt iratkozunk fel a vissza-gomb eseményére.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // 1. LÉPÉS: A ViewModel fogadása és beállítása DataContext-ként
            // Ez feltételezi, hogy a ViewService a ViewModel-t az "e.Parameter"-ben adja át.
            if (e.Parameter is ViewModelBase viewModel)
            {
                this.DataContext = viewModel;
            }

            // 2. LÉPÉS: Hívjuk az ős-osztály metódusát
            base.OnNavigatedTo(e);

            // 3. LÉPÉS: Feliratkozás a rendszer-vissza gombra (mobilos "vissza" nyíl)
            SystemNavigationManager.GetForCurrentView().BackRequested += GamePage_BackRequested;
        }

        /// <summary>
        /// Akkor hívódik meg, amikor elhagyjuk ezt az oldalt.
        /// Itt kell leiratkozni az eseményről, hogy elkerüljük a memóriaszivárgást.
        /// </summary>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // 4. LÉPÉS: Leiratkozás a rendszer-vissza gombról
            SystemNavigationManager.GetForCurrentView().BackRequested -= GamePage_BackRequested;
        }

        /// <summary>
        /// Ez a metódus fut le, amikor a felhasználó megnyomja a rendszer "vissza" gombját.
        /// Ez helyettesíti a "Főoldalra" gombot.
        /// </summary>
        private void GamePage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            // Ellenőrizzük, hogy a navigációs "Frame" tud-e egyáltalán visszalépni
            if (Frame.CanGoBack)
            {
                // 5. LÉPÉS: Fontos! Jelezzük a rendszernek, hogy mi kezeltük az eseményt.
                // Így az OS nem fogja alapértelmezetten bezárni az appot.
                e.Handled = true;

                // 6. LÉPÉS: Végrehajtjuk a visszalépést (a Főoldalra).
                Frame.GoBack();
            }
        }
    }
}