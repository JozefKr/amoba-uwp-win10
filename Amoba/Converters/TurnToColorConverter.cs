using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Amoba.Converters
{
    /// <summary>
    /// Logikai értéket (bool) konvertál SolidColorBrush ecsetté,
    /// tipikusan egy felhasználói felületi elem (pl. háttere) színének beállítására.
    /// </summary>
    public class TurnToColorConverter : IValueConverter
    {
        /// <summary>
        /// Konvertál egy logikai (bool) értéket SolidColorBrush objektummá.
        /// </summary>
        /// <param name="value">A konvertálandó érték. Elvárt típus: bool.</param>
        /// <param name="targetType">A cél típusa (nem használt, elvárt: SolidColorBrush).</param>
        /// <param name="parameter">Opcionális konverter paraméter (nem használt).</param>
        /// <param name="language">A használandó nyelv (nem használt).</param>
        /// <returns>
        /// Egy <see cref="SolidColorBrush"/> objektum:
        /// <list type="bullet">
        /// <item><description>Ha a 'value' <see langword="true"/>, a szín: sötétzöld (#548E19).</description></item>
        /// <item><description>Ha a 'value' <see langword="false"/>, a szín: fehér (<see cref="Colors.White"/>).</description></item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentException">Ha a bemeneti 'value' nem bool típusú.</exception>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool)
            {
                var val = value as bool?;

                if (val.Value)
                {
                    return new SolidColorBrush(Color.FromArgb(255, 84, 142, 25));
                }
                else return new SolidColorBrush(Colors.White);
            }
            else throw new ArgumentException("Value must be bool type");
        }

        /// <summary>
        /// Nem valósítja meg a konverziót visszafelé.
        /// </summary>
        /// <param name="value">A konvertálandó érték (nem használt).</param>
        /// <param name="targetType">A cél típusa (nem használt).</param>
        /// <param name="parameter">Opcionális konverter paraméter (nem használt).</param>
        /// <param name="language">A használandó nyelv (nem használt).</param>
        /// <returns>
        /// Mindig <see cref="DependencyProperty.UnsetValue"/>-t ad vissza.
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
