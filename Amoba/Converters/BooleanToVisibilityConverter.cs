using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Amoba.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // Ha a 'value' (a ViewModel-ből jövő bool) true, akkor 'Visible',
            // különben 'Collapsed' (elrejtve).
            return (value is bool boolValue && boolValue) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // A visszaalakításra itt nincs szükségünk
            return (value is Visibility visibility && visibility == Visibility.Visible);
        }
    }
}