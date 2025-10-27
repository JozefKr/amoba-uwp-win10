using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Amoba.Converters
{
    /// <summary>
    /// Egy bool értéket (pl. IsPlayer1Turn) konvertál SolidColorBrush-sá.
    /// "Bombabiztos" a null és UnsetValue értékekre, hogy elkerülje a XAML összeomlást.
    /// </summary>
    public class TurnToColorConverter : IValueConverter
    {
        // Alapértelmezett színek
        private static readonly SolidColorBrush ActiveColor = new SolidColorBrush(Colors.Red);
        private static readonly SolidColorBrush InactiveColor = new SolidColorBrush(Colors.Gray);

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // 1. Kezeljük azt az esetet, amikor a DataContext még nem töltődött be
            if (value == null || value == DependencyProperty.UnsetValue)
            {
                // Visszaadunk egy biztonságos alapértelmezett színt
                return InactiveColor;
            }

            try
            {
                // 2. Biztonságos konvertálás
                bool isTurn = (bool)value;
                return isTurn ? ActiveColor : InactiveColor;
            }
            catch (Exception)
            {
                // Ha a konverzió bármi másért hibázik,
                // egy biztonságos alapértelmezett színt adunk vissza
                return InactiveColor;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
