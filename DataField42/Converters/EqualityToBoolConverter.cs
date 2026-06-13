using System.Globalization;

namespace DataField42.Converters;

public class EqualityToBoolConverter : ConverterBase
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.Equals(parameter) ?? false;
}
