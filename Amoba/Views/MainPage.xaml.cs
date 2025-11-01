using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Amoba.Views
{
    /// <summary>
    /// A főoldal (MainPage) code-behind logikája.
    /// Kezeli az animációkat és a "Vissza" gomb viselkedését.
    /// </summary>
    public sealed partial class MainPage : BasePage
    {
        public MainPage()
        {
            this.InitializeComponent();

            // Az animáció indítását áthelyeztük az OnNavigatedTo-ba,
            // hogy biztosan csak a betöltődés után fusson le.
        }

        /// <summary>
        /// Felülírja az alapértelmezett "Vissza" gomb viselkedését.
        /// </summary>
        protected override void OnBackRequested(BackRequestedEventArgs e)
        {
            // Ha a Frame előzményei üresek (vagyis a Főoldalon vagyunk
            // és nincs hova visszalépni)...
            if (!Frame.CanGoBack)
            {
                // ...akkor a "Vissza" gomb bezárja az alkalmazást.
                e.Handled = true; // Jelezzük, hogy kezeltük az eseményt
                CoreApplication.Exit();
            }
            else
            {
                // Ha valamiért mégis van hova visszalépni,
                // engedélyezzük az alapértelmezett visszalépést.
                base.OnBackRequested(e);
            }
        }

        /// <summary>
        /// Akkor hívódik meg, amikor az oldal láthatóvá válik.
        /// Ez a helyes pont az indító animációk elindítására.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Elindítjuk a XAML-ben definiált animációkat
            FadeOutLogoStoryboard.Begin();
            FadeInMenuStoryboard.Begin();
        }
    }
}