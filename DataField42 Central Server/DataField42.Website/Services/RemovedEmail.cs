using System.Net;

namespace DataField42.Website.Services;

/// <summary>
/// Renders readme text that has had its email addresses removed. The text is escaped first and
/// only the placeholder is turned into markup, so an author's own angle brackets stay text.
/// </summary>
public static class RemovedEmail
{
    private const string Notice = "<span class='hiddenText'>#Email removed by DataField42#</span>";

    /// <summary>
    /// The text as HTML, with each removed address shown as a notice.
    /// </summary>
    public static string Highlight(string text) =>
        WebUtility.HtmlEncode(text).Replace(TextFormatting.RemovedEmailPlaceholder, Notice);
}
