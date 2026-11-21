namespace DataField42.Website.Models;

/// <summary>
/// One mod on the mods page.
/// </summary>
public sealed class ModViewModel
{
    public required string FolderName { get; init; }

    public string? FullName { get; init; }

    public string? Version { get; init; }

    public DateTime? LastUpdate { get; init; }

    public int MapCount { get; init; }

    /// <summary>
    /// The other mods this one needs installed, by folder name.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];
}
