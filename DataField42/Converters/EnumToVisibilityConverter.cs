using System.Globalization;
using System.Windows;

namespace DataField42.Converters;

public class EnumToVisibilityConverter : ConverterBase
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Enum enumValue && parameter is string targetName
            && Enum.TryParse(value.GetType(), targetName, out var targetValue))
            return enumValue.Equals(targetValue) ? Visibility.Visible : Visibility.Collapsed;

        return Visibility.Collapsed;
    }
}
