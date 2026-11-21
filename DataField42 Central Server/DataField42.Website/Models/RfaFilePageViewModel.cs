using DataField42.Website.Services.SyntaxHighlighting;

namespace DataField42.Website.Models;

/// <summary>
/// One file out of an archive. Scripts and shaders are syntax-highlighted; anything else textual
/// is shown as it is.
/// </summary>
public sealed record RfaFilePageViewModel
{
    public required int RfaId { get; init; }

    public string? ArchivePath { get; init; }

    public required string FilePath { get; init; }

    /// <summary>
    /// The highlighted file, or null when it is not a format with a highlighter.
    /// </summary>
    public IReadOnlyList<HighlightedLine>? HighlightedLines { get; init; }

    /// <summary>
    /// The file as plain text, or the reason it could not be shown.
    /// </summary>
    public string? Text { get; init; }
}
