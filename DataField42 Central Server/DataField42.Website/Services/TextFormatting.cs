using System.Text;
using System.Text.RegularExpressions;

namespace DataField42.Website.Services;

/// <summary>
/// The text transformations the pages share: map-name title casing, durations, and the
/// placeholder that stands in for an email address removed from a readme.
/// </summary>
public static partial class TextFormatting
{
    /// <summary>
    /// What is left in a readme where an email address was removed.
    /// </summary>
    public const string RemovedEmailPlaceholder = "datafield42hiddenemail";

    private static readonly HashSet<string> LowercaseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "as", "at", "but", "by", "en", "for", "from", "if",
        "in", "nor", "of", "on", "or", "per", "the", "to", "v", "vs", "via", "with",
    };

    private static readonly HashSet<string> UppercaseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "GC", "DC", "DCF", "TL", "GP", "DS",
    };

    [GeneratedRegex("""((?:\s|-|"|[\[({<])+)""")]
    private static partial Regex WordBoundary { get; }

    [GeneratedRegex(@"[A-Za-z0-9\.\-+_]+@[A-Za-z0-9\.\-+_]+\.[A-Za-z]+")]
    private static partial Regex EmailAddress { get; }

    /// <summary>
    /// Title-cases a map or mod name: small words stay lowercase, known abbreviations go all
    /// caps, and every other word gets one leading capital.
    /// </summary>
    public static string Title(string? value, bool allowCasingForUnknownWords = false)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var result = new StringBuilder(value.Length);
        foreach (var chunk in WordBoundary.Split(value))
            result.Append(TitleWord(chunk, allowCasingForUnknownWords));
        return result.ToString();
    }

    private static string TitleWord(string chunk, bool allowCasingForUnknownWords)
    {
        if (chunk.Length == 0)
            return chunk;
        if (LowercaseWords.Contains(chunk))
            return chunk.ToLowerInvariant();
        if (UppercaseWords.Contains(chunk))
            return chunk.ToUpperInvariant();

        string tail = chunk[1..];
        return char.ToUpperInvariant(chunk[0]) + (allowCasingForUnknownWords ? tail : tail.ToLowerInvariant());
    }

    /// <summary>
    /// Replaces every email address with <see cref="RemovedEmailPlaceholder"/>, which the views
    /// render as a notice. Readmes from the early 2000s are full of addresses their authors never
    /// expected to be republished.
    /// </summary>
    public static string RemoveEmailAddresses(string text) =>
        EmailAddress.Replace(text, RemovedEmailPlaceholder);

    /// <summary>
    /// A count with its noun in the right number: <c>1 mod</c>, <c>2 mods</c>. A noun with an
    /// irregular plural names it.
    /// </summary>
    public static string Count(int count, string singular, string? plural = null) =>
        $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";

    /// <summary>
    /// A duration as <c>mm:ss</c>, or <c>hh:mm:ss</c> once it reaches an hour.
    /// </summary>
    public static string Duration(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, (int)seconds));
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }

    /// <summary>
    /// The short name of a gameplay mode, as the round list shows it.
    /// </summary>
    public static string GameplayModeShort(string? gameplayMode) => gameplayMode?.ToLowerInvariant() switch
    {
        "coop" => "Coop",
        "conquest" => "CQ",
        "ctf" => "CTF",
        "tdm" => "TDM",
        "objectivemode" => "Objective",
        _ => gameplayMode ?? string.Empty,
    };
}
