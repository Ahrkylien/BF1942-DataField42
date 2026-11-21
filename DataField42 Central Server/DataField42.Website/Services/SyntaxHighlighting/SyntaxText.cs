using System.Net;

namespace DataField42.Website.Services.SyntaxHighlighting;

/// <summary>
/// Renders a highlighted token as HTML. The game's scripts are indented with tabs, which a
/// browser collapses, so each tab becomes the three spaces the source lines up with.
/// </summary>
public static class SyntaxText
{
    private const string Tab = "&nbsp;&nbsp;&nbsp;";

    public static string ToHtml(string text) =>
        WebUtility.HtmlEncode(text).Replace("\t", Tab);
}
