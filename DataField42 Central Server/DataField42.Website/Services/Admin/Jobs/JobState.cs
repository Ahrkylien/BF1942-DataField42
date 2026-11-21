namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Where a job run has got to.
/// </summary>
public enum JobState
{
    /// <summary>
    /// Never run.
    /// </summary>
    Idle,

    Running,

    Succeeded,

    Failed,

    Cancelled,

    /// <summary>
    /// The server stopped while the job was running, so it neither finished nor was stopped. Only
    /// ever read back from storage: nothing in a live process can be interrupted.
    /// </summary>
    Interrupted,
}
