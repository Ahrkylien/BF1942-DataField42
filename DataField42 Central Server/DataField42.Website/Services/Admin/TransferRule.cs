namespace DataField42.Website.Services.Admin;

/// <summary>
/// One rule out of an uploaded rule set.
/// </summary>
public sealed record TransferRule(TransferRuleKind Kind, string Path, string? DestinationPath, DateTime? ModifiedDate)
{
    /// <summary>
    /// What the rule is spelled as in the file, kept so an unrecognised rule can be reported by
    /// the name it was written under.
    /// </summary>
    public required string RawKind { get; init; }
}
