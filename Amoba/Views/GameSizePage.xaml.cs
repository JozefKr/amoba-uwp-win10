using Amoba.ViewModel;
using Windows.UI.Xaml.Navigation;

namespace Amoba.Views
{
    public sealed partial class GameSizePage : BasePage
    {
        public GameSizePage()
        {
            this.InitializeComponent();
        }

        // ===================================================================
        // JAVÍTÁS ITT: Felülbíráljuk az OnNavigatedTo-t
        // ===================================================================
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // 1. FONTOS: Először hívjuk meg a BasePage logikáját!
            // Ez állítja be a DataContext-et és kezeli a vissza-gombot.
            base.OnNavigatedTo(e);

            // 2. Most, hogy a DataContext be van állítva, elérhetjük a ViewModel-t.
            // Ellenőrizzük, hogy a DataContext valóban a mi ViewModel-ünk-e.
            if (this.DataContext is GameSizeViewModel viewModel)
            {
                // 3. Állítsuk vissza az 'Enabled' tulajdonságot 'true'-ra.
                // Ez újra engedélyezi a gombokat a XAML-ben.
                viewModel.Enabled = true;
            }
        }
    }
}