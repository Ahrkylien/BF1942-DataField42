namespace DataField42.Website.Models;

/// <summary>
/// Everything the map page shows.
/// </summary>
public sealed class MapDetailsViewModel
{
    public required int MapId { get; init; }

    public required string Name { get; init; }

    public string? AuthorName { get; init; }

    public string? ModFolderName { get; init; }

    public string? ModFullName { get; init; }

    public string? GameMapId { get; init; }

    public required string InGameMapUrl { get; init; }

    /// <summary>
    /// The depth of the playable area in metres, shown as a scale bar beside the minimap.
    /// </summary>
    public int? WorldHeight { get; init; }

    public IReadOnlyList<ControlPointViewModel> ControlPoints { get; init; } = [];

    /// <summary>
    /// The distinct vehicles and equipment the level's spawners can produce.
    /// </summary>
    public IReadOnlyList<string> ObjectSpawns { get; init; } = [];

    public IReadOnlyList<MapTagViewModel> Tags { get; init; } = [];

    public IReadOnlyList<MapRfaViewModel> Rfas { get; init; } = [];

    public IReadOnlyList<string> ImageUrls { get; init; } = [];

    public IReadOnlyList<BattleCraftPathViewModel> BattleCraftPaths { get; init; } = [];

    /// <summary>
    /// The same level name under other mods, which is how a map's conversions are found.
    /// </summary>
    public IReadOnlyList<(int MapId, string? ModFullName)> MapsFromOtherMods { get; init; } = [];

    public string? MultiplayerBriefingObjectives { get; init; }

    public string? ReadmeText { get; init; }

    public bool HasConquest { get; init; }

    public bool HasCaptureTheFlag { get; init; }

    public bool HasTeamDeathmatch { get; init; }

    public bool HasCoop { get; init; }

    public bool HasObjectiveMode { get; init; }

    public bool IsAdmin { get; init; }
}
