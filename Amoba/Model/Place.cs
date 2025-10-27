using GalaSoft.MvvmLight;
using System;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Amoba.Model
{
    // VÁLTOZÁS: A Place modellnek is jeleznie kell a változásait (INotifyPropertyChanged)
    // A ViewModelBase-ből öröklés a legegyszerűbb módja ennek a GalaSoft-tal.
    public class Place : ViewModelBase
    {
        private IconType? type;
        private bool isEmpty = true;
        private ImageSource image;

        public Place()
        {
            // Az IsEmpty alapértelmezett értéke már 'true' a backing field miatt
        }

        public int Id { get; set; }

        public ImageSource Image
        {
            get => image;
            private set => Set(ref image, value); // Használjuk a Set-et a UI frissítéséhez
        }

        public bool IsEmpty
        {
            get => isEmpty;
            set
            {
                Set(ref isEmpty, value);
                // Frissítjük a 'SetImage' parancs CanExecute állapotát
                RaisePropertyChanged(nameof(IsEmpty));
            }
        }

        public IconType? Type
        {
            get { return type; }
            set
            {
                // Használjuk a Set-et, hogy a UI biztosan frissüljön
                Set(ref type, value);

                // Az Image property frissítése (logika a régiből)
                if (value == IconType.Circle)
                {
                    Image = new BitmapImage(new Uri("ms-appx:///Assets/Images/circle.png"));
                }
                else if (value == IconType.Cross)
                {
                    Image = new BitmapImage(new Uri("ms-appx:///Assets/Images/cross.png"));
                }
                else
                {
                    Image = null;
                }
            }
        }
    }
}
