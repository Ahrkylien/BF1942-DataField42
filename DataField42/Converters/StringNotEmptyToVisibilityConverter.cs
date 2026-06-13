using System.Globalization;
using System.Windows;

namespace DataField42.Converters;

public class StringNotEmptyToVisibilityConverter : ConverterBase
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;
}
