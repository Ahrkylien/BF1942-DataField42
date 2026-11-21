namespace DataField42.Website.Data.Entities;

/// <summary>
/// One run of an admin job, kept so that what a job did outlives the process that ran it.
/// </summary>
public class AdminJobRun
{
    public int Id { get; set; }

    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// <c>Running</c>, <c>Succeeded</c>, <c>Failed</c>, <c>Cancelled</c> or <c>Interrupted</c>, the
    /// last being a run whose server stopped before it ended.
    /// </summary>
    public string State { get; set; } = string.Empty;

    public DateTime StartedUtc { get; set; }

    public DateTime? FinishedUtc { get; set; }

    /// <summary>
    /// What the run was limited to, such as <c>mod=bf1942</c>.
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// The phase the run had reached when it was last saved.
    /// </summary>
    public string? Step { get; set; }

    public int CompletedCount { get; set; }

    public int TotalCount { get; set; }

    public string? Error { get; set; }

    public string Log { get; set; } = string.Empty;
}
