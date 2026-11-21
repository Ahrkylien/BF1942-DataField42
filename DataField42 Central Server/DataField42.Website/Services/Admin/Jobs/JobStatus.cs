namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// A job's state as the admin page sees it.
/// </summary>
public sealed record JobStatus(
    string JobId,
    string Name,
    string Description,
    JobState State,
    string? Step,
    double? Percentage,
    int CompletedCount,
    int TotalCount,
    DateTime? StartedUtc,
    DateTime? FinishedUtc,
    string? Error,
    IReadOnlyList<string> Log)
{
    public bool IsRunning => State == JobState.Running;
}
