namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Where a run is at a moment, in the form that is stored.
/// </summary>
public sealed record JobRunSnapshot(
    JobState State,
    string? Step,
    int CompletedCount,
    int TotalCount,
    DateTime? FinishedUtc,
    string? Error,
    string Log);
