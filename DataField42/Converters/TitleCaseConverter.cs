using System.Globalization;
using System.Text.RegularExpressions;

namespace DataField42.Converters;

public class TitleCaseConverter : ConverterBase
{
    private static readonly HashSet<string> DoNotTitle = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "as", "at", "but", "by", "en", "for", "from", "if",
        "in", "nor", "of", "on", "or", "per", "the", "to", "v", "vs", "via", "with"
    };

    private static readonly HashSet<string> AllCaps = new(StringComparer.OrdinalIgnoreCase)
    {
        "GC", "DC", "DCF", "TL", "GP", "DS"
    };

    // Splits on runs of whitespace, hyphens, quotes, and opening brackets — keeping the separators
    private static readonly Regex _split = new(@"((?:\s|-|""|[\[({<])+)");

    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrEmpty(s)) return value ?? string.Empty;
        return Title(s);
    }

    public static string Title(string value)
    {
        var chunks = _split.Split(value);
        return string.Concat(Array.ConvertAll(chunks, TitleWord));
    }

    private static string TitleWord(string chunk)
    {
        if (chunk.Length == 0) return chunk;
        if (DoNotTitle.Contains(chunk)) return chunk.ToLower();
        if (AllCaps.Contains(chunk)) return chunk.ToUpper();
        return char.ToUpper(chunk[0]) + chunk[1..].ToLower();
    }
}
