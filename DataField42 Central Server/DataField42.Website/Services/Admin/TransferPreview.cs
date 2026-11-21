namespace DataField42.Website.Services.Admin;

/// <summary>
/// What an uploaded rule set would do, for review before it is applied.
/// </summary>
public sealed class TransferPreview
{
    public required IReadOnlyList<TransferRule> Rules { get; init; }

    public int DeleteCount => Rules.Count(rule => rule.Kind == TransferRuleKind.Delete);

    public int MoveCount => Rules.Count(rule => rule.Kind == TransferRuleKind.Move);

    public int CopyCount => Rules.Count(rule => rule.Kind == TransferRuleKind.Copy);

    public int AddCount => Rules.Count(rule => rule.Kind == TransferRuleKind.Add);

    public int ChangeDateCount => Rules.Count(rule => rule.Kind == TransferRuleKind.ChangeModifiedDate);

    public int UnknownCount => Rules.Count(rule => rule.Kind == TransferRuleKind.Unknown);

    /// <summary>
    /// Files the rules add that the uploaded archive does not contain, which would leave the
    /// collection short of those files.
    /// </summary>
    public IReadOnlyList<string> MissingFromArchive { get; init; } = [];

    /// <summary>
    /// Files the rules move or copy that are not where the rules expect them, which would leave
    /// the collection short of those files.
    /// </summary>
    public IReadOnlyList<string> MissingSources { get; init; } = [];

    /// <summary>
    /// Destinations that already hold a file, which the apply step refuses to overwrite.
    /// </summary>
    public IReadOnlyList<string> OccupiedDestinations { get; init; } = [];

    public bool HasWarnings => MissingFromArchive.Count > 0 || MissingSources.Count > 0 || OccupiedDestinations.Count > 0 || UnknownCount > 0;

    public DateTime UploadedUtc { get; init; }

    public long ArchiveSize { get; init; }

    public int ArchiveEntryCount { get; init; }
}
