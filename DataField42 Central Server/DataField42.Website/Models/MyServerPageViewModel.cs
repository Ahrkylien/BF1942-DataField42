namespace DataField42.Website.Models;

/// <summary>
/// Maps grouped by the player who played them, from the server manager's activity log.
/// </summary>
public sealed class MyServerPageViewModel
{
    public IReadOnlyList<(string GroupKey, IReadOnlyList<string> Maps)> MapGroups { get; init; } = [];
}
