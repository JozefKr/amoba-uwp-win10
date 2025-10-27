using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Amoba.Converters
{
    /// <summary>
    /// Egy "int" (BoardSize) értéket konvertál a játéktábla teljes szélességévé/magasságává.
    /// "Bombabiztos" a null és 0 értékekre, hogy elkerülje a XAML összeomlását.
    /// </summary>
    public class PlacesToDimensionsConverter : IValueConverter
    {
        // 1 elem (Gomb + Margó) teljes szélessége (80 + 5*2) = 90
        private const double ItemTotalWidth = 90.0;

        // Alapértelmezett méret (pl. 3x3-as tábla), ha a DataContext még null
        private const double DefaultDimension = 270.0; // 3 * 90

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // 1. Kezeljük, ha a DataContext még nem töltődött be (null vagy UnsetValue)
            if (value == null || value == DependencyProperty.UnsetValue)
            {
                // Visszaadunk egy érvényes, NEM NULLA méretet
                return DefaultDimension;
            }

            try
            {
                // 2. Biztonságos konvertálás int-té
                int boardSize = System.Convert.ToInt32(value);

                // 3. Kezeljük, ha a BoardSize 0
                if (boardSize > 0)
                {
                    // Kiszámítjuk a valós méretet
                    return (double)(boardSize * ItemTotalWidth);
                }
                else
                {
                    // Ha a BoardSize 0, akkor is alapértelmezett méretet ad vissza
                    return DefaultDimension;
                }
            }
            catch (Exception)
            {
                // Ha a 'value' valamiért nem konvertálható (pl. string),
                // akkor is visszaadunk egy biztonságos méretet
                return DefaultDimension;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
