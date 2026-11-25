namespace DataField42.Website.Models;

/// <summary>
/// A piece of a finding on the integrity page: plain text, or text that links to a page of the site.
/// </summary>
public sealed record FindingPart(string Text, string? Url = null)
{
    public static implicit operator FindingPart(string text) => new(text);

    /// <summary>
    /// A link to a map's page, which reads as the map's name unless <paramref name="text"/> says otherwise.
    /// </summary>
    public static FindingPart Map(int id, string? name, string? text = null) =>
        new(text ?? (string.IsNullOrWhiteSpace(name) ? $"map {id}" : name), $"/Map/{id}");

    /// <summary>
    /// A link to an archive's page, which reads as the archive's id.
    /// </summary>
    public static FindingPart Rfa(int id) => new(id.ToString(), $"/RFA/{id}");
}
