namespace DataField42.Website.Models;

/// <summary>
/// The scraping overview, sorted so each map's listings from different sites sit together.
/// </summary>
public sealed class ScrapingPageViewModel
{
    public IReadOnlyList<ScrapedPageViewModel> Pages { get; init; } = [];

    /// <summary>
    /// How many of the listed maps are not in the collection yet.
    /// </summary>
    public int MissingCount { get; init; }
}
