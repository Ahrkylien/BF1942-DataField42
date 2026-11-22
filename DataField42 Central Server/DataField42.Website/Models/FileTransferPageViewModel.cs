using DataField42.Website.Services.Admin;
using DataField42.Website.Services;
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

    /// <summary>
    /// The four steps of the transfer and where each stands.
    /// </summary>
    public IReadOnlyList<TransferStep> Steps =>
    [
        ServerFileList is { } list
            ? new("List this server's files", Check, TransferDisplay.Green, $"Written {TextFormatting.Stamp(list.ModifiedUtc)}")
            : new("List this server's files", "1", TransferDisplay.Yellow, "To do"),
        Rules is { } rules
            ? new("Upload what the workstation made", Check, TransferDisplay.Green, $"{(Archive is null ? "Rules" : "Rules and archive")}, {TextFormatting.Stamp(rules.ModifiedUtc)}")
            : new("Upload what the workstation made", "2", TransferDisplay.Yellow, "To do"),
        ReviewStep,
        ApplyStep,
    ];

    private const string Check = "✓";

    private TransferStep ReviewStep => Preview switch
    {
        null => new("Review the changes", "3", TransferDisplay.Grey, "Waiting for the upload"),
        { IsApplied: true } => new("Review the changes", Check, TransferDisplay.Green, "Applied"),
        { ErrorCount: > 0 } preview => new("Review the changes", "✗", TransferDisplay.Red, TextFormatting.Count(preview.ErrorCount, "error")),
        { WarningCount: > 0 } preview => new("Review the changes", "⚠", TransferDisplay.Yellow, TextFormatting.Count(preview.WarningCount, "warning")),
        _ => new("Review the changes", Check, TransferDisplay.Green, "No problems"),
    };

    private TransferStep ApplyStep => Preview switch
    {
        null => new("Apply", "4", TransferDisplay.Grey, "Waiting for the upload"),
        { AppliedUtc: { } appliedUtc } => new("Apply", Check, TransferDisplay.Green, $"Applied {TextFormatting.Stamp(appliedUtc)}"),
        { ErrorCount: > 0 } => new("Apply", "✗", TransferDisplay.Red, "Blocked"),
        _ => new("Apply", "4", TransferDisplay.Yellow, "Ready to apply"),
    };

    public string? Message { get; init; }

    public string? ErrorMessage { get; init; }
}
