namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// A long-running maintenance task started from the admin page.
/// </summary>
public interface IAdminJob
{
    /// <summary>
    /// The identifier the admin page starts this job by.
    /// </summary>
    string JobId { get; }

    /// <summary>
    /// What the button says.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// What the job does, shown beside the button.
    /// </summary>
    string Description { get; }

    Task RunAsync(JobContext jobContext, CancellationToken cancellationToken);
}
