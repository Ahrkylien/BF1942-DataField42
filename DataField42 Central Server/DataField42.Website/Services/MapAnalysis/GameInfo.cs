namespace DataField42.Website.Services.MapAnalysis;

/// <summary>
/// The <c>game.*</c> metadata a mod's <c>init.con</c> and a level's scripts declare.
/// </summary>
public sealed class GameInfo
{
    public string? MapId { get; internal set; }

    public string? CustomGameName { get; internal set; }

    public string? CustomGameVersion { get; internal set; }

    public string? MultiplayerBriefingObjectives { get; internal set; }

    public string? ObjectiveBriefing { get; internal set; }

    /// <summary>
    /// The playable area as <c>minX</c>, <c>minZ</c>, <c>width</c>, <c>depth</c> -- the frame the
    /// minimap's control-point markers are positioned within.
    /// </summary>
    public int[]? ActiveCombatArea { get; internal set; }

    /// <summary>
    /// Every <c>game.addModPath</c> in declaration order, e.g. <c>Mods/BF1942/</c>.
    /// </summary>
    public List<string> ModPaths { get; } = [];
}
