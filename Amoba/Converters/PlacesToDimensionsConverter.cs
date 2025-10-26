using System;
using System.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Amoba.Converters
{
    /// <summary>
    /// Egy <see cref="ICollection"/> (gyűjtemény) elemszámát konvertálja
    /// egy négyzetes elrendezéshez szükséges dimenzió (pl. szélesség vagy magasság) értékké.
    /// A számítás képlete: négyzetgyök(elemszám) * 90.
    /// Tipikusan egy amőba vagy rácsos játék méreteinek dinamikus beállítására használható.
    /// </summary>
    public class PlacesToDimensionsConverter : IValueConverter
    {
        /// <summary>
        /// Kiszámítja a dimenziót egy gyűjtemény elemszáma alapján.
        /// </summary>
        /// <param name="value">A konvertálandó gyűjtemény. Elvárt típus: <see cref="ICollection"/>.</param>
        /// <param name="targetType">A cél típusa (nem használt, elvárt: numeric).</param>
        /// <param name="parameter">Opcionális konverter paraméter (nem használt).</param>
        /// <param name="language">A használandó nyelv (nem használt).</param>
        /// <returns>
        /// Egy <see cref="double"/> típusú dimenzióérték, ami a <c>Math.Sqrt(elemszám) * 90</c> képlet alapján jön létre.
        /// </returns>
        /// <exception cref="ArgumentException">Ha a bemeneti 'value' nem implementálja az <see cref="ICollection"/> interfészt.</exception>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ICollection)
            {
                var count = (value as ICollection).Count;
                return Math.Sqrt(count) * 90;
            }
            else throw new ArgumentException("Parameter must implement ICollection interface");
        }

        /// <summary>
        /// Nem valósítja meg a konverziót visszafelé, mivel a dimenzióból a gyűjtemény nagyságát nem lehet egyértelműen visszavezetni.
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
