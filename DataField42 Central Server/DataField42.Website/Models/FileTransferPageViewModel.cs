using DataField42.Website.Services.Admin;
using DataField42.Website.Services.Admin.Jobs;

namespace DataField42.Website.Models;

/// <summary>
/// The file transfer page: what this server has listed, what has been uploaded, and what applying
/// it would do.
/// </summary>
public sealed class FileTransferPageViewModel
{
    /// <summary>
    /// The stage 1 file list, once it has been produced.
    /// </summary>
    public UploadedFileViewModel? ServerFileList { get; init; }

    public UploadedFileViewModel? Rules { get; init; }

    public UploadedFileViewModel? Archive { get; init; }

    /// <summary>
    /// What the uploaded rules would do, or null when no rules have been uploaded.
    /// </summary>
    public TransferPreview? Preview { get; init; }

    public JobStatus? FileListJob { get; init; }

    public JobStatus? ApplyJob { get; init; }

    public string? Message { get; init; }

    public string? ErrorMessage { get; init; }
}
