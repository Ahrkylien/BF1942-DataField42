using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace DataField42.Converters
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var booleanToVisibilityConverter = new BooleanToVisibilityConverter();
            return booleanToVisibilityConverter.Convert(!(bool)value, targetType, parameter, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
