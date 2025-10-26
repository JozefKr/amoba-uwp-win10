using System;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Amoba.Model
{
    public class Place
    {
        private IconType? type;

        public Place()
        {
            IsEmpty = true;
        }

        public int Id { get; set; }

        public ImageSource Image { get; private set; }

        public bool IsEmpty { get; set; }

        public IconType? Type
        {
            get { return type; }
            set
            {
                if (type == IconType.Circle)
                {
                    // A BitmapImage egy ImageSource, így közvetlenül hozzárendelhető.
                    Image = new BitmapImage(new Uri("ms-appx:///Images/circle.png"));
                }
                else if (type == IconType.Cross)
                {
                    Image = new BitmapImage(new Uri("ms-appx:///Images/cross.png"));
                }
                else
                {
                    // Ha nincs ikon, az ImageSource nullázható.
                    Image = null;
                }
            }
        }

    }
}
