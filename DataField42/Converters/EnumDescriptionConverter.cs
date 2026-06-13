using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace DataField42.Converters;

public class EnumDescriptionConverter : ConverterBase
{
    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        var field = value.GetType().GetField(value.ToString()!);
        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString() ?? string.Empty;
    }
}
