namespace DataField42.Website.Models;

/// <summary>
/// The map summary the maps list and the server page both show: minimap, author, gameplay modes,
/// and either the author's briefing text or the start of their readme.
/// </summary>
public sealed class MapBannerViewModel
{
    public required int MapId { get; init; }

    public required string Name { get; init; }

    public string? AuthorName { get; init; }

    public string? ModFolderName { get; init; }

    public string? ModFullName { get; init; }

    public required string InGameMapUrl { get; init; }

    public IReadOnlyList<MapTagViewModel> Tags { get; init; } = [];

    public string? MultiplayerBriefingObjectives { get; init; }

    public string? ReadmeText { get; init; }

    public bool HasConquest { get; init; }

    public bool HasCaptureTheFlag { get; init; }

    public bool HasTeamDeathmatch { get; init; }

    public bool HasCoop { get; init; }

    public bool HasObjectiveMode { get; init; }

    /// <summary>
    /// The release date, or a range when the map's archives were not all built on the same day.
    /// </summary>
    public string? DateText { get; init; }

    /// <summary>
    /// Marks a map with no readme, shown only to signed-in admins as something still to be filled
    /// in.
    /// </summary>
    public bool ShowHasNoReadme { get; init; }
}
