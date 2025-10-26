using Amoba.ViewModel;
using Windows.UI.Xaml.Controls;


namespace Amoba.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GameSizeDialog : ContentDialog
    {
        private GameSizeViewModel ViewModel { get; set; }
        public GameSizeDialog()
        {
            this.InitializeComponent();

            // 1. Létrehozzuk a ViewModel-t, átadva neki az Action-t, ami a bezárást végrehajtja.
            // A "this.Hide()" hívása zárja be a ContentDialog-ot.
            ViewModel = new GameSizeViewModel(this.Hide);

            // 2. Beállítjuk a DataContext-et.
            this.DataContext = ViewModel;
        }

        // A dialog eredményének lekérdezése, pl. a fő ablakban
        public int GetSelectedSize()
        {
            return ViewModel.SelectedGameSize;
        }
    }
}
