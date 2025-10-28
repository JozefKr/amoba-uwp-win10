using GalaSoft.MvvmLight;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Amoba.Views
{
    /// <summary>
    /// Egy egyedi ős-oldal, ami tartalmazza az összes közös logikát:
    /// 1. ViewModel automatikus beállítása a DataContext-nek.
    /// 2. A rendszer-vissza gomb (<-) kezelésének automatikus fel- és leiratkoztatása.
    /// </summary>
    public class BasePage : Page // Nem "sealed partial", csak "class"!
    {
        /// <summary>
        /// Amikor az oldalra navigálunk: ViewModel beállítása + Vissza-gomb feliratkozás.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // 1. LÉPÉS: A ViewModel fogadása és beállítása DataContext-ként
            if (e.Parameter is ViewModelBase viewModel)
            {
                this.DataContext = viewModel;
            }

            base.OnNavigatedTo(e);

            // 2. LÉPÉS: Feliratkozás a rendszer-vissza gombra
            SystemNavigationManager.GetForCurrentView().BackRequested += BasePage_BackRequested;
        }

        /// <summary>
        /// Amikor elhagyjuk az oldalt: Leiratkozás.
        /// </summary>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // 3. LÉPÉS: Leiratkozás a rendszer-vissza gombról
            SystemNavigationManager.GetForCurrentView().BackRequested -= BasePage_BackRequested;
        }

        /// <summary>
        /// Ez a központi eseménykezelő, ami továbbhív a "virtual" metódusra.
        /// </summary>
        private void BasePage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            // 4. LÉPÉS: Meghívjuk a felülbírálható metódust
            OnBackRequested(e);
        }

        /// <summary>
        /// EZ A KULCS: Egy "virtual" (felülbírálható) metódus.
        /// Az alapértelmezett viselkedés a "Visszalépés".
        /// Az oldalaink (pl. MainPage) felülbírálhatják ezt a logikát.
        /// </summary>
        protected virtual void OnBackRequested(BackRequestedEventArgs e)
        {
            // Alapértelmezett viselkedés: lépjünk vissza, ha lehet.
            if (Frame.CanGoBack)
            {
                e.Handled = true;
                Frame.GoBack();
            }
        }
    }
}