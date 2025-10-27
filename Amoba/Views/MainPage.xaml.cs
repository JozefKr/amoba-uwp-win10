using GalaSoft.MvvmLight;
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
            // Az e.Parameter tartalmazza a ViewService által átadott ViewModel példányt.
            if (e.Parameter is ViewModelBase viewModel)
            {
                this.DataContext = viewModel;
            }
            base.OnNavigatedTo(e);
        }
    }
}
