namespace DataField42.Website.Models;

/// <summary>
/// How far along a scraped download is in being added to the collection.
/// </summary>
public enum ScrapedPageStatus
{
    /// <summary>
    /// Not in the collection: still to be fetched.
    /// </summary>
    Missing,

    /// <summary>
    /// In the collection, but with no readme recorded.
    /// </summary>
    InCollectionWithoutReadme,

    /// <summary>
    /// In the collection with its readme.
    /// </summary>
    Complete,

    /// <summary>
    /// The same map as the row above, listed again by another site.
    /// </summary>
    Duplicate,
}
