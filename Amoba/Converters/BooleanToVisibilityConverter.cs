using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Amoba.Converters
{
    /// <summary>
    /// Konvertál egy bool értéket Visibility.Visible vagy Visibility.Collapsed értékké.
    /// Támogatja az "Invert" paramétert a logika megfordításához.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isVisible = (value is bool boolValue && boolValue);

            // Ellenőrizzük, hogy a XAML-ben átadtuk-e az "Invert" paramétert
            if (parameter != null && parameter.ToString().ToLower() == "invert")
            {
                isVisible = !isVisible;
            }

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // A visszaalakításra itt nincs szükségünk
            return (value is Visibility visibility && visibility == Visibility.Visible);
        }
    }
}