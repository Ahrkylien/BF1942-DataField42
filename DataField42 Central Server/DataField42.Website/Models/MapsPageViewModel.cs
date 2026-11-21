namespace DataField42.Website.Models;

/// <summary>
/// The maps list: the filter form, the page links, and the banners themselves.
/// </summary>
public sealed class MapsPageViewModel
{
    public required string SelectedModFolderName { get; init; }

    public required string MapNameFilter { get; init; }

    public required string AuthorFilter { get; init; }

    public required GameplayModeFilter GameplayModes { get; init; }

    public required int ActivePage { get; init; }

    /// <summary>
    /// The page numbers to link to. A zero stands for the gap between runs of pages, rendered as
    /// an ellipsis.
    /// </summary>
    public IReadOnlyList<int> PageNumbers { get; init; } = [];

    public IReadOnlyList<(string FolderName, string? FullName)> Mods { get; init; } = [];

    public IReadOnlyList<string> Authors { get; init; } = [];

    public IReadOnlyList<MapBannerViewModel> Maps { get; init; } = [];
}
