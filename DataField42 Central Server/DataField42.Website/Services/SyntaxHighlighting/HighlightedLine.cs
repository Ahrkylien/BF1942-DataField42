namespace DataField42.Website.Services.SyntaxHighlighting;

/// <summary>
/// One highlighted source line.
/// </summary>
public sealed class HighlightedLine
{
    private readonly List<SyntaxPart> _parts = [];

    public IReadOnlyList<SyntaxPart> Parts => _parts;

    internal void Add(string text, string cssClass) => _parts.Add(new SyntaxPart(text, cssClass));

    internal void AddRange(IEnumerable<SyntaxPart> parts) => _parts.AddRange(parts);
}
