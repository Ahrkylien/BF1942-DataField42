using System.Globalization;
using System.Text.RegularExpressions;
namespace DataField42.Website.Services.SyntaxHighlighting;

/// <summary>
/// Splitting a source line the way the game's own parser does, shared by the script and shader
/// highlighters.
/// </summary>
internal static partial class SourceLine
{
    [GeneratedRegex(@"^[\t\f ]*([^\t\f \.]*)(?:\.([^\t\f \n]*))?[\t\f ]*(.*)", RegexOptions.Singleline)]
    private static partial Regex CommandPattern { get; }

    [GeneratedRegex("""(?:"(?:(?:.*?")|(?:.*)))|(?:[^\t\f ]+)""")]
    private static partial Regex ArgumentPattern { get; }

    /// <summary>
    /// Splits <paramref name="line"/> into its class name, method and arguments. Unlike the
    /// script engine's own parser, quotes are kept: the highlighter renders the source as written.
    /// </summary>
    internal static (string? ClassName, string? Method, List<string> Arguments) Parse(string line)
    {
        var match = CommandPattern.Match(line);
        if (!match.Success)
            return (null, null, []);

        string? className = match.Groups[1].Value is { Length: > 0 } name ? name : null;
        string? method = match.Groups[2].Success && match.Groups[2].Value is { Length: > 0 } m ? m : null;

        var arguments = new List<string>();
        if (match.Groups[3].Value is { Length: > 0 } argumentText)
            foreach (var argument in ArgumentPattern.Matches(argumentText))
                arguments.Add(((Match)argument).Value);

        return (className, method, arguments);
    }

    /// <summary>
    /// Whether a token is one of the game's numeric literals.
    /// </summary>
    internal static bool IsNumber(string value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
}
