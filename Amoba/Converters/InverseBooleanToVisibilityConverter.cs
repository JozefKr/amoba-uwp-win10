using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Amoba.Converters
{
    // Igaz -> Collapsed (Rejtett), Hamis -> Visible (Látható)
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool boolValue && boolValue) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (value is Visibility visibility && visibility == Visibility.Collapsed);
        }
    }
}