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
        // ===================================================================
        private static readonly ImageSource ImageCircle = new BitmapImage(new Uri("ms-appx:///Assets/Images/circle.png"));
        private static readonly ImageSource ImageCross = new BitmapImage(new Uri("ms-appx:///Assets/Images/cross.png"));
        // ===================================================================

        private bool _isEmpty;
        private IconType _type;
        private ImageSource _image;
        private int _id;

        private bool _isWinningCell = false;

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
                if (Set(ref _type, value))
                {
                    UpdateImageAndState(value);
                }
            }
        }

        /// <summary>
        /// Igaz, ha ez a cella része a győztes sornak.
        /// A GameViewModel állítja be a játék végén.
        /// </summary>
        public bool IsWinningCell
        {
            get => _isWinningCell;
            // A Set() biztosítja, hogy a XAML azonnal frissüljön
            set => Set(ref _isWinningCell, value);
        }
        // ===================================================================


        // Segédmetódus a kép és IsEmpty állapot frissítésére
        private void UpdateImageAndState(IconType newType)
        {
            // Amikor a Típus változik, az IsEmpty állapota automatikusan követi.
            IsEmpty = (newType == IconType.None);

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
            clone.Type = this.Type;

            // Fontos: Az 'IsWinningCell'-t NEM kell klónozni,
            // mivel az egy tisztán UI-szintű vizuális állapot,
            // az AI logikájának (GameLogic) nincs rá szüksége.

            return clone;
        }
    }
}