using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Amoba.Converters
{
    /// <summary>
    /// Egy 'bool' értéket (pl. IsMine) HorizontalAlignment-é alakít.
    /// Igaz -> Jobbra (Right)
    /// Hamis -> Balra (Left)
    /// </summary>
    public class BooleanToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool isMine && isMine) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
