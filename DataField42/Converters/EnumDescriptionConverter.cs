using System.Globalization;

namespace DataField42.Converters;

public class EnumDescriptionConverter : ConverterBase
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is Enum enumValue ? enumValue.Description() : string.Empty;
}
