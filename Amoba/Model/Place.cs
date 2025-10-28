using GalaSoft.MvvmLight; // Szükséges a ViewModelBase miatt
using System;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Amoba.Model
{
    // Kiterjesztettük ViewModelBase-szel, hogy tudjon változásértesítést küldeni
    // ICloneable interfészt ELTÁVOLÍTOTTUK
    public class Place : ViewModelBase
    {
        private bool _isEmpty;
        private IconType _type; // Nullable helyett alapértelmezett None
        private ImageSource _image;
        private int _id;

        public Place()
        {
            _isEmpty = true;
            _type = IconType.None; // Alapértelmezett érték
        }

        public int Id
        {
            get => _id;
            set => Set(ref _id, value);
        }

        // Image csak olvasható kívülről, a Type setter állítja be
        public ImageSource Image
        {
            get => _image;
            private set => Set(ref _image, value);
        }

        public bool IsEmpty
        {
            get => _isEmpty;
            set => Set(ref _isEmpty, value);
        }

        public IconType Type
        {
            get => _type;
            set
            {
                // A Set metódus kezeli a RaisePropertyChanged-et
                if (Set(ref _type, value))
                {
                    // Ha a típus változik, frissítjük a képet és az IsEmpty állapotot
                    UpdateImageAndState(value);
                }
            }
        }

        // Segédmetódus a kép és IsEmpty állapot frissítésére
        private void UpdateImageAndState(IconType newType)
        {
            IsEmpty = (newType == IconType.None); // Csak akkor üres, ha None

            if (newType == IconType.Circle)
            {
                Image = new BitmapImage(new Uri("ms-appx:///Assets/Images/circle.png"));
            }
            else if (newType == IconType.Cross)
            {
                Image = new BitmapImage(new Uri("ms-appx:///Assets/Images/cross.png"));
            }
            else // None esetén
            {
                Image = null;
            }
        }

        // SAJÁT Clone metódus, ami nem használ ICloneable interfészt
        // JAVÍTVA: A publikus property-ket használjuk a beállításhoz
        public Place ClonePlace()
        {
            var clone = new Place();
            clone.Id = this.Id;
            // Fontos: Itt a Type property setterét kell hívni,
            // hogy az UpdateImageAndState is lefusson a klónon!
            clone.Type = this.Type;
            // Az IsEmpty a Type setterében beállítódik, de explicit is beállíthatjuk
            clone._isEmpty = this._isEmpty; // Itt a privát mező direkt beállítása még szükséges lehet
                                            // mert a Type setter az IsEmpty-t a newType alapján állítja

            return clone;
        }
    }
}