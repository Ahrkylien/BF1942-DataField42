namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Stage 2 of the file transfer: applies the uploaded rules and unpacks the uploaded archive.
/// </summary>
public sealed class ApplyTransferJob(FileTransferService fileTransfer) : IAdminJob
{
    public string JobId => "ApplyTransfer";

    public string Name => "Stage 2: apply the transfer";

    public string Description =>
        "Applies the uploaded rules -- moving, copying, deleting and re-dating files -- and unpacks the uploaded archive.";

    public Task RunAsync(JobContext jobContext, CancellationToken cancellationToken) =>
        fileTransfer.ApplyAsync(jobContext, cancellationToken);
}
