using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Amoba.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : BasePage
    {
        public MainPage()
        {
            this.InitializeComponent();

            // Animáció indítása ---
            // Hivatkozzunk a XAML-ben elnevezett Storyboardra és indítsuk el.
            // Győződj meg róla, hogy a 'using Windows.UI.Xaml.Media.Animation;' hozzá van adva!
            (Resources["FadeOutLogoStoryboard"] as Storyboard)?.Begin();
        }

        protected override void OnBackRequested(BackRequestedEventArgs e)
        {
            // A SPECIÁLIS viselkedés:
            if (!Frame.CanGoBack) // Ha már nem lehet visszalépni (mert mi vagyunk a főoldal)
            {
                // Akkor lépjünk ki!
                e.Handled = true;
                CoreApplication.Exit();
            }
            else
            {
                // Ez egy vészhelyzet (ha valahogy mégis idejutottunk, de van hova visszamenni)
                // Hívjuk az alap viselkedést (a BasePage.OnBackRequested-et)
                base.OnBackRequested(e);
            }
        }
    }
}
