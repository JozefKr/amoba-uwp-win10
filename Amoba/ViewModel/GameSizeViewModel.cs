using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Windows.Input;

namespace Amoba.ViewModel
{
    public class GameSizeViewModel : ViewModelBase
    {
        // Ezzel hívjuk meg a ContentDialog.Hide() metódusát.
        private readonly Action _closeDialogAction;

        private ICommand selectSize;
        private int selectedGameSize;
        private bool enabled;

        // A ContentDialog.xaml.cs a GetSelectedSize() metódusban ezt a property-t használja.
        public int SelectedGameSize
        {
            get => selectedGameSize;
            // A Set metódus a GalaSoft.MvvmLight.ViewModelBase része.
            private set => Set(ref selectedGameSize, value);
        }

        public bool Enabled
        {
            get { return enabled; }
            set
            {
                // Használhatja a GalaSoft Set metódusát is, de a RaisePropertyChanged is működik.
                enabled = value;
                RaisePropertyChanged();
            }
        }

        // ÚJ KONSTRUKTOR: Felveszi a bezárásért felelős Action-t.
        public GameSizeViewModel(Action closeDialogAction)
        {
            // Eltároljuk a ContentDialog.Hide metódus hivatkozását.
            _closeDialogAction = closeDialogAction;
            Enabled = true;
            // A selectSize parancs inicializálása
            SelectSize = new RelayCommand<string>(SelectSizeMethod);
        }

        // Megjegyzés: Az alapértelmezett, paraméter nélküli konstruktort eltávolítottam.

        public ICommand SelectSize
        {
            get
            {
                // Már inicializálva van a konstruktorban.
                return selectSize;
            }
            private set
            {
                selectSize = value;
            }
        }

        private void SelectSizeMethod(string size)
        {
            if (int.TryParse(size, out int selectedSize))
            {
                // 1. Tároljuk a kiválasztott méretet
                SelectedGameSize = selectedSize;

                // 2. Bezárjuk a ContentDialog-ot az átadott Action hívásával (ez hívja a Hide()-ot).
                _closeDialogAction?.Invoke();

                // FONTOS: Az eredeti Messenger.Default.Send hívást eltávolítottam, 
                // mivel a bezárás most már a ContentDialogResult.GetSelectedSize() útján történik.
            }
        }
    }
}
