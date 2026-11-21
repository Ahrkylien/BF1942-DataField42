namespace DataField42.Website.Models;

/// <summary>
/// Everything the integrity check found, grouped by the kind of problem.
/// </summary>
public sealed class ErrorListPageViewModel
{
    public IReadOnlyList<IntegrityFindingGroup> Groups { get; init; } = [];
}
