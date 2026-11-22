namespace DataField42.Website.Services.Admin;

/// <summary>
/// What applying the uploaded rules would do, and what is wrong with them.
/// </summary>
public sealed class TransferPreview
{
    private IReadOnlyList<TransferChange>? _changes;

    public required IReadOnlyList<TransferRule> Rules { get; init; }

    public required TransferArchive Archive { get; init; }

    /// <summary>
    /// Everything found wrong or doubtful. Empty for a rule set that has already been applied, since
    /// it can no longer be checked against the collection it was built for.
    /// </summary>
    public IReadOnlyList<TransferIssue> Issues { get; init; } = [];

    /// <summary>
    /// When these rules were applied, or <see langword="null"/> if they have not been.
    /// </summary>
    public DateTime? AppliedUtc { get; init; }

    public DateTime UploadedUtc { get; init; }

    /// <summary>
    /// The modification date of a file in the collection, or <see langword="null"/> if it is not there.
    /// </summary>
    public Func<string, DateTime?> LastWrite { get; init; } = _ => null;

    /// <summary>
    /// The rules read as one change per file, ordered by path, each with its own findings.
    /// </summary>
    public IReadOnlyList<TransferChange> Changes => _changes ??= TransferChangeBuilder.Build(Rules, Issues, LastWrite);

    /// <summary>
    /// The findings about the uploads as a whole, which belong to no file.
    /// </summary>
    public IEnumerable<TransferIssue> UploadIssues => Issues.Where(issue => issue.RuleIndex is null && issue.Path.Length == 0);

    public int ErrorCount => Issues.Count(issue => issue.Severity == TransferIssueSeverity.Error);

    public int WarningCount => Issues.Count(issue => issue.Severity == TransferIssueSeverity.Warning);

    public bool IsApplied => AppliedUtc is not null;

    /// <summary>
    /// Why the rules cannot be applied, or <see langword="null"/> when they can.
    /// </summary>
    public string? BlockedReason =>
        IsApplied ? "These rules were applied already. Upload new ones."
        : ErrorCount > 0 ? $"Blocked: fix the {TextFormatting.Count(ErrorCount, "error")} in step 3 first."
        : Rules.Count == 0 ? "There are no rules to apply."
        : null;
}
