namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// One run in a list of runs, without its log, which can be large.
/// </summary>
public sealed record JobRunSummary(
    int Id,
    string State,
    DateTime StartedUtc,
    DateTime? FinishedUtc,
    string? Parameters,
    int CompletedCount,
    int TotalCount,
    string? Error);
