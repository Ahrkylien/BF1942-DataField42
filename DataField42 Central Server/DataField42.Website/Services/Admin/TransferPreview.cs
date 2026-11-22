namespace DataField42.Website.Services.Admin;

/// <summary>
/// What applying the uploaded rules would do, and what is wrong with them.
/// </summary>
public sealed class TransferPreview
{
    public required IReadOnlyList<TransferRule> Rules { get; init; }

    /// <summary>
    /// Everything found wrong or doubtful, errors first. Empty for a rule set that has already been
    /// applied, since it can no longer be checked against the collection it was built for.
    /// </summary>
    public IReadOnlyList<TransferIssue> Issues { get; init; } = [];

    /// <summary>
    /// When these rules were applied, or <see langword="null"/> if they have not been.
    /// </summary>
    public DateTime? AppliedUtc { get; init; }

    public int DeleteCount => Rules.Count(rule => rule.Kind == TransferRuleKind.Delete);

    public int MoveCount => Rules.Count(rule => rule.Kind == TransferRuleKind.Move);

    public int CopyCount => Rules.Count(rule => rule.Kind == TransferRuleKind.Copy);

    public int AddCount => Rules.Count(rule => rule.Kind == TransferRuleKind.Add);

    public int ChangeDateCount => Rules.Count(rule => rule.Kind == TransferRuleKind.ChangeModifiedDate);

    public int UnknownCount => Rules.Count(rule => rule.Kind == TransferRuleKind.Unknown);

    public int ErrorCount => Issues.Count(issue => issue.Severity == TransferIssueSeverity.Error);

    public int WarningCount => Issues.Count(issue => issue.Severity == TransferIssueSeverity.Warning);

    public bool HasErrors => ErrorCount > 0;

    public bool IsApplied => AppliedUtc is not null;

    /// <summary>
    /// Whether the rules may be applied: there are some, they have no errors, and they have not been
    /// applied already.
    /// </summary>
    public bool CanApply => Rules.Count > 0 && !HasErrors && !IsApplied;

    public DateTime UploadedUtc { get; init; }

    public long ArchiveSize { get; init; }

    public int ArchiveEntryCount { get; init; }

    public bool ArchiveUploaded { get; init; }
}
