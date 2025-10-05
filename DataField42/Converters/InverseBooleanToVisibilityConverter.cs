using System.Globalization;

namespace DataField42.Converters;

public class InverseBooleanToVisibilityConverter : BooleanToVisibilityConverter
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return base.Convert(!(bool)value, targetType, parameter, culture);
    }
}
