namespace DataField42.Website.Models;

/// <summary>
/// One group of findings on the integrity page.
/// </summary>
public sealed record IntegrityFindingGroup(string Title, IReadOnlyList<string> Findings);
