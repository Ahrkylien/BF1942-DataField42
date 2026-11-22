using DataField42.Website.Services.Admin;
using Microsoft.AspNetCore.Html;

namespace DataField42.Website.Models;

/// <summary>
/// How the file transfer page shows things: the colours of the site, and what each kind of change
/// is called and marked with.
/// </summary>
public static class TransferDisplay
{
    public const string Green = "#57a857";
    public const string Yellow = "#c9b037";
    public const string Red = "#ab0909";
    public const string Grey = "#8a8a8a";

    public sealed record KindStyle(string Label, string Letter, string Color);

    /// <summary>
    /// The kinds of change, in the order the counters list them.
    /// </summary>
    public static IReadOnlyList<TransferChangeKind> Kinds { get; } =
    [
        TransferChangeKind.Added, TransferChangeKind.Replaced, TransferChangeKind.Deleted,
        TransferChangeKind.Moved, TransferChangeKind.Copied, TransferChangeKind.Redated, TransferChangeKind.NotUnderstood,
    ];

    public static KindStyle Style(TransferChangeKind kind) => kind switch
    {
        TransferChangeKind.Added => new("Added", "A", Green),
        TransferChangeKind.Replaced => new("Replaced", "M", "#d98f3d"),
        TransferChangeKind.Deleted => new("Deleted", "D", Red),
        TransferChangeKind.Moved => new("Moved", "R", "#6e9bd1"),
        TransferChangeKind.Copied => new("Copied", "C", "#4fb3b3"),
        TransferChangeKind.Redated => new("Date changed", "T", Grey),
        _ => new("Not understood", "?", Red),
    };

    /// <summary>
    /// A path that may wrap, but only after a slash, with its folder dimmed so the file name stands out.
    /// </summary>
    public static IHtmlContent Path(string path, bool strike = false)
    {
        int cut = path.LastIndexOf('/') + 1;
        string folder = System.Net.WebUtility.HtmlEncode(path[..cut]).Replace("/", "/<wbr>");
        string name = System.Net.WebUtility.HtmlEncode(path[cut..]);
        string decoration = strike ? " ft-struck" : "";
        return new HtmlString($"<span class='ft-folder{decoration}'>{folder}</span><span class='ft-name{decoration}'>{name}</span>");
    }
}
