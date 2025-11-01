using GalaSoft.MvvmLight;
using System;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Amoba.Model
{
    public class Place : ViewModelBase
    {
        // ===================================================================
        // (Teljesítmény): Kép-gyorsítótárazás
        // A képeket csak egyszer hozzuk létre, amikor az osztály betöltődik.
        // Ez (főleg az AI klónozása miatt) rengeteg memóriát és processzoridőt spórol.
        // ===================================================================
        private static readonly ImageSource ImageCircle = new BitmapImage(new Uri("ms-appx:///Assets/Images/circle.png"));
        private static readonly ImageSource ImageCross = new BitmapImage(new Uri("ms-appx:///Assets/Images/cross.png"));
        // ===================================================================

        private bool _isEmpty;
        private IconType _type;
        private ImageSource _image;
        private int _id;

        public Place()
        {
            _isEmpty = true;
            _type = IconType.None;
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
            // A 'private set' tökéletes eltokozás!
            private set => Set(ref _image, value);
        }

        public bool IsEmpty
        {
            get => _isEmpty;
            // A 'private set' itt is jó lenne, de a 'public' sem hiba,
            // mivel az UpdateImageAndState hívja meg.
            // Maradjunk a 'Set' használatánál, ahogy írtad:
            set => Set(ref _isEmpty, value);
        }

        public IconType Type
        {
            get => _type;
            set
            {
                // A 'Set' biztosítja, hogy a logika csak akkor fusson le, ha
                // az érték tényleg változott.
                if (Set(ref _type, value))
                {
                    UpdateImageAndState(value);
                }
            }
        }

        // Segédmetódus a kép és IsEmpty állapot frissítésére
        private void UpdateImageAndState(IconType newType)
        {
            // Amikor a Típus változik, az IsEmpty állapota automatikusan követi.
            IsEmpty = (newType == IconType.None);

            // 'switch' használata olvashatóbb, mint az 'if-else if-else'
            // A 'new BitmapImage' helyett a statikus mezőket használjuk.
            switch (newType)
            {
                case IconType.Circle:
                    Image = ImageCircle;
                    break;
                case IconType.Cross:
                    Image = ImageCross;
                    break;
                case IconType.None:
                default:
                    Image = null;
                    break;
            }
        }

        /// <summary>
        /// Létrehozza ennek a 'Place'-nek egy memóriabeli másolatát (klónját),
        /// főleg az AI algoritmus (Minimax) számára.
        /// </summary>
        public Place ClonePlace()
        {
            var clone = new Place();
            clone.Id = this.Id;

            // A 'Type' beállítása automatikusan beállítja
            // az 'Image' és 'IsEmpty' tulajdonságokat a klónon is
            // az 'UpdateImageAndState' metóduson keresztül.
            clone.Type = this.Type;

            // TÖRÖLVE: A 'clone._isEmpty = this._isEmpty;' sor felesleges,
            // mert a 'clone.Type' settere már beállította az 'IsEmpty'-t.

            return clone;
        }
    }
}