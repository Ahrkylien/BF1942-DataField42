using System.Globalization;
using System.Text.RegularExpressions;

namespace DataField42.Converters;

public class CamelCaseSpacesConverter : ConverterBase
{
    private static readonly Regex _split = new Regex(@"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");

    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        return _split.Replace(value.ToString()!, " ");
    }
}
