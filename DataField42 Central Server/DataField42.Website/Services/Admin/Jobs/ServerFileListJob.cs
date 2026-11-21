namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Stage 1 of the file transfer: lists what this server holds, so the workstation can work out
/// what has changed.
/// </summary>
public sealed class ServerFileListJob(FileTransferService fileTransfer) : IAdminJob
{
    public string JobId => "ServerFileList";

    public string Name => "Stage 1: list server files";

    public string Description =>
        "Walks the mods directory and writes a checksummed list of every file, for the workstation to compare against. "
        + "This reads every byte of the collection, so it takes a while.";

    public Task RunAsync(JobContext jobContext, CancellationToken cancellationToken) =>
        fileTransfer.BuildServerFileListAsync(jobContext, cancellationToken);
}
