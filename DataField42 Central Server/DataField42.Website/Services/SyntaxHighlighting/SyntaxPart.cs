namespace DataField42.Website.Services.SyntaxHighlighting;

/// <summary>
/// A run of text and the CSS class it is coloured with.
/// </summary>
public readonly record struct SyntaxPart(string Text, string CssClass);
