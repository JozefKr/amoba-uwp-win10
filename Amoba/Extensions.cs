using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Amoba
{
    public static class Extensions
    {
        // Ez a metódus átveszi az adatfolyamot, és betölti azt BitmapImage-ként.
        // Mivel az adatfolyamok UWP-ben aszinkron módon töltődnek be, a metódus is async.
        public static async Task<ImageSource> ConvertToBitmapImage(this IRandomAccessStream stream)
        {
            if (stream == null) return null;

            // stream.Seek(0); // Állítsa vissza az adatfolyam pozícióját a nullára, ha szükséges

            var bitmapImage = new BitmapImage();

            // Aszinkron betöltés
            await bitmapImage.SetSourceAsync(stream);

            return bitmapImage;
        }
    }
}
