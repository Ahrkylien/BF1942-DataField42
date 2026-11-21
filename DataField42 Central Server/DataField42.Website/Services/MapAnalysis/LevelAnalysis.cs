using BF1942.Core.Scripting;

using BF1942.FileSystem;

namespace DataField42.Website.Services.MapAnalysis;

/// <summary>
/// The result of reading one level: its scripts' data, the archive stack they were read from, and
/// anything that failed along the way.
/// </summary>
public sealed class LevelAnalysis
{
    public required LevelData Data { get; init; }

    public required IBfFileSystem FileSystem { get; init; }

    public required GameplayModes GameplayModes { get; init; }

    public required IReadOnlyList<ScriptError> Errors { get; init; }

    /// <summary>
    /// Level archives whose internal level folder does not match the map name the archive is
    /// filed under -- almost always a repackaged map that was never renamed internally.
    /// </summary>
    public required IReadOnlyList<string> MismatchedArchiveNames { get; init; }
}
