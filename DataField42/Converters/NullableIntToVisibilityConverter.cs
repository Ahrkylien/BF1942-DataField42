using System.Globalization;
using System.Windows;

namespace DataField42.Converters;

public class NullableToVisibilityConverter : ConverterBase
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;
}
