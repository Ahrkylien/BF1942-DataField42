namespace DataField42.Website.Models;

/// <summary>
/// One finding on the integrity page, as the pieces it is written in.
/// </summary>
public sealed class IntegrityFinding(params FindingPart[] parts)
{
    public IReadOnlyList<FindingPart> Parts { get; } = parts;
}
