using System.Globalization;

namespace DataField42.Converters;

public class BooleanToVisibilityConverter : ConverterBase
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var booleanToVisibilityConverter = new System.Windows.Controls.BooleanToVisibilityConverter();
        return booleanToVisibilityConverter.Convert((bool)value, targetType, parameter, culture);
    }
}
