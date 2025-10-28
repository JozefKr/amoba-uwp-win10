using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

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
