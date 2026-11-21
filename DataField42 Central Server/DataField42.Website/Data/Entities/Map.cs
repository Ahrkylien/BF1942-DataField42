namespace DataField42.Website.Data.Entities;

/// <summary>
/// One level of one mod. Most columns are filled in by the map analysis job rather than by hand,
/// and are null until it has run for this map.
/// </summary>
public class Map
{
    public int Id { get; set; }

    /// <summary>
    /// The display name: <see cref="NameRaw"/> with underscores replaced by spaces.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The level's name as it appears on disk and inside its archives.
    /// </summary>
    public string? NameRaw { get; set; }

    public int? AuthorId { get; set; }

    /// <summary>
    /// The folder the level's archives sit in, relative to the mod folder, or null when they sit
    /// directly in the mod's own levels directory.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// The readme file inside <see cref="Folder"/>, relative to it.
    /// </summary>
    public string? Readme { get; set; }

    /// <summary>
    /// Screenshots found beside the archives, as a JSON array of paths relative to
    /// <see cref="Folder"/>.
    /// </summary>
    public string? Images { get; set; }

    public int? GpmConquest { get; set; }

    public int? GpmCaptureTheFlag { get; set; }

    public int? GpmTeamDeathmatch { get; set; }

    public int? GpmCoop { get; set; }

    public int? GpmObjectiveMode { get; set; }

    public int? ModId { get; set; }

    /// <summary>
    /// The combat area and control points as JSON: <c>[[minX, minZ, width, depth],
    /// [[name, team, uncapturable, [x, y, z]], ...]]</c>.
    /// </summary>
    public string? ControlPointsInfo { get; set; }

    /// <summary>
    /// The level's object spawners as JSON: <c>[[[x, y, z], osId, team, templateName,
    /// {team: spawnedTemplate}], ...]</c>.
    /// </summary>
    public string? ObjectSpawnsInfo { get; set; }

    /// <summary>
    /// The mod the level's own <c>init.con</c> declares it belongs to. A value that differs from
    /// the mod's folder name means the level was copied between mods without being updated.
    /// </summary>
    public string? GameMapId { get; set; }

    public string? MultiplayerBriefingObjectives { get; set; }

    public DateTime? DateOldestRfa { get; set; }

    public DateTime? DateNewestRfa { get; set; }

    public Mod? Mod { get; set; }

    public Author? Author { get; set; }

    public ICollection<Rfa> Rfas { get; set; } = [];

    public ICollection<TagLink> TagLinks { get; set; } = [];

    public bool HasConquest => GpmConquest == 1;

    public bool HasCaptureTheFlag => GpmCaptureTheFlag == 1;

    public bool HasTeamDeathmatch => GpmTeamDeathmatch == 1;

    public bool HasCoop => GpmCoop == 1;

    public bool HasObjectiveMode => GpmObjectiveMode == 1;
}
