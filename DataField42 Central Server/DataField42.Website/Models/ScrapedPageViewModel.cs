namespace DataField42.Website.Models;

/// <summary>
/// One map download found while scraping the sites that host them.
/// </summary>
public sealed class ScrapedPageViewModel
{
    public string? Source { get; init; }

    public string? Title { get; init; }

    public string? FileName { get; init; }

    public string? Link1 { get; init; }

    public string? Link2 { get; init; }

    public string? Link3 { get; init; }

    public string? Date { get; init; }

    public string? FileSize { get; init; }

    public string? FileSizeOld { get; init; }

    public string? Downloads { get; init; }

    public string? OtherSource { get; init; }

    public ScrapedPageStatus Status { get; set; }
}
