namespace DataField42.Website.Data.Entities;

/// <summary>
/// One Refractor Flat Archive belonging to a map: either the map itself or one of its patches.
/// </summary>
public class Rfa
{
    public int Id { get; set; }

    /// <summary>
    /// Path to the archive, relative to its mod's folder. Case-sensitive in the database, because
    /// the server the files live on is too.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// The trailing <c>_nnn</c> of the file name, or null for the map's base archive. Higher
    /// patches take priority over lower ones.
    /// </summary>
    public int? PatchNumber { get; set; }

    public string? Note { get; set; }

    public string? Crc32 { get; set; }

    /// <summary>
    /// Whether clients need this archive; server-only archives are not distributed.
    /// </summary>
    public int? Client { get; set; }

    /// <summary>
    /// Whether this archive is part of the version of the map that is handed out.
    /// </summary>
    public int? Recommended { get; set; }

    public int? MapId { get; set; }

    /// <summary>
    /// The archive's modification time on disk, which is as close to a release date as these
    /// files carry.
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// The author's own BattleCraft project path, recovered from a <c>rem (...)</c> comment the
    /// editor leaves in the level's scripts.
    /// </summary>
    public string? BattleCraftLevelPath { get; set; }

    public Map? Map { get; set; }

    public bool IsClient => Client == 1;

    public bool IsRecommended => Recommended == 1;

    public string FileName => System.IO.Path.GetFileName(Path?.Replace('\\', '/') ?? string.Empty);
}
