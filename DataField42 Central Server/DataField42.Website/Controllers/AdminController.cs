using DataField42.Website.Data;
using DataField42.Website.Models;
using DataField42.Website.Security;
using DataField42.Website.Services.Admin;
using DataField42.Website.Services.Admin.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataField42.Website.Controllers;

/// <summary>
/// The admin pages: the maintenance jobs, and the file transfer between this server and the
/// workstation the collection is sorted on.
/// </summary>
[Authorize(Policy = AdminAuthentication.PolicyName)]
[Route("/Admin")]
public sealed class AdminController(
    DataField42DbContext context,
    JobRunner jobRunner,
    JobRunStore runStore,
    FileTransferService fileTransfer) : Controller
{
    /// <summary>
    /// The largest upload accepted, which has to fit a full collection update.
    /// </summary>
    private const long MaxUploadSize = 32L * 1024 * 1024 * 1024;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Admin";
        ViewData["ActiveNav"] = "Admin";

        return View(new AdminPageViewModel
        {
            Jobs = jobRunner.GetStatuses(),
            ModFolderNames = await context.Mods
                .AsNoTracking()
                .Where(mod => mod.FolderName != null)
                .OrderBy(mod => mod.FolderName)
                .Select(mod => mod.FolderName!)
                .ToListAsync(cancellationToken),
            Message = TempData["Message"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string,
        });
    }

    /// <summary>
    /// Starts a job. The admin pages call this with <c>fetch</c> and read the job's new state from
    /// the answer; a plain form post, from a browser without script, still redirects back.
    /// </summary>
    [HttpPost("Jobs/{jobId}/Start")]
    [ValidateAntiForgeryToken]
    public IActionResult StartJob(string jobId, [FromForm] string? mod, [FromForm] string? returnTo)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(mod))
            parameters["mod"] = mod.Trim();

        bool started = jobRunner.TryStart(jobId, parameters, out string? error);

        if (WantsJson)
            return started ? Json(jobRunner.GetStatus(jobId)) : Conflict(new { error });

        if (started)
            TempData["Message"] = "Started.";
        else
            TempData["ErrorMessage"] = error;

        return Redirect(GetSafeReturnUrl(returnTo));
    }

    [HttpPost("Jobs/{jobId}/Cancel")]
    [ValidateAntiForgeryToken]
    public IActionResult CancelJob(string jobId, [FromForm] string? returnTo)
    {
        bool cancelled = jobRunner.TryCancel(jobId);

        if (WantsJson)
            return cancelled ? Json(jobRunner.GetStatus(jobId)) : Conflict(new { error = "That job is not running." });

        if (cancelled)
            TempData["Message"] = "Stopping after the current item.";
        else
            TempData["ErrorMessage"] = "That job is not running.";

        return Redirect(GetSafeReturnUrl(returnTo));
    }

    /// <summary>
    /// The state of every job, polled by the admin pages while a job runs.
    /// </summary>
    [HttpGet("Jobs/Status")]
    public IActionResult JobStatus() => Json(jobRunner.GetStatuses());

    /// <summary>
    /// The runs of one job that are kept, newest first.
    /// </summary>
    [HttpGet("Jobs/{jobId}/Runs")]
    public async Task<IActionResult> JobRuns(string jobId, CancellationToken cancellationToken)
    {
        if (jobRunner.GetName(jobId) is not { } name)
            return NotFound();

        ViewData["Title"] = name;
        ViewData["ActiveNav"] = "Admin";

        return View(new JobRunsPageViewModel
        {
            JobId = jobId,
            JobName = name,
            Runs = await runStore.ListAsync(jobId, cancellationToken),
        });
    }

    /// <summary>
    /// The whole log of one run, as text.
    /// </summary>
    [HttpGet("Runs/{runId:int}/Log")]
    public async Task<IActionResult> RunLog(int runId, bool header = true, CancellationToken cancellationToken = default)
    {
        var run = await runStore.FindAsync(runId, cancellationToken);
        if (run is null)
            return NotFound();

        string headerLine = $"{jobRunner.GetName(run.JobId) ?? run.JobId}, started {JobRunStore.AsUtc(run.StartedUtc).ToLocalTime():yyyy-MM-dd HH:mm:ss}"
            + $", {run.State}{(run.Parameters is null ? "" : $" ({run.Parameters})")}";

        return Content($"{header}\n\n{run.Log}\n", "text/plain; charset=utf-8");
    }

    [HttpGet("FileTransfer")]
    public async Task<IActionResult> FileTransfer(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "File transfer";
        ViewData["ActiveNav"] = "Admin";

        return View(new FileTransferPageViewModel
        {
            ServerFileList = ToViewModel(fileTransfer.ServerFileListInfo),
            Rules = ToViewModel(fileTransfer.RulesInfo),
            Archive = ToViewModel(fileTransfer.ArchiveInfo),
            Preview = await fileTransfer.PreviewAsync(cancellationToken),
            FileListJob = jobRunner.GetStatus("ServerFileList"),
            ApplyJob = jobRunner.GetStatus("ApplyTransfer"),
            Message = TempData["Message"] as string,
            ErrorMessage = TempData["ErrorMessage"] as string,
        });
    }

    /// <summary>
    /// Hands the stage 1 file list to the workstation.
    /// </summary>
    [HttpGet("FileTransfer/ServerFileList")]
    public IActionResult DownloadServerFileList()
    {
        if (fileTransfer.ServerFileListInfo is null)
        {
            TempData["ErrorMessage"] = "There is no file list yet. Run stage 1 first.";
            return RedirectToAction(nameof(FileTransfer));
        }

        return PhysicalFile(fileTransfer.ServerFileListPath, "application/json", FileTransferService.ServerFileListName);
    }

    /// <summary>
    /// Takes the rules and the archive the workstation produced. Nothing is applied here: the
    /// upload is followed by a preview, and only then by stage 2.
    /// </summary>
    [HttpPost("FileTransfer/Upload")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadSize)]
    [RequestSizeLimit(MaxUploadSize)]
    public async Task<IActionResult> Upload(IFormFile? rules, IFormFile? archive, CancellationToken cancellationToken)
    {
        if (rules is null && archive is null)
        {
            TempData["ErrorMessage"] = "Choose a rules file, an archive, or both.";
            return RedirectToAction(nameof(FileTransfer));
        }

        var uploaded = new List<string>();

        if (rules is { Length: > 0 })
        {
            await using var stream = rules.OpenReadStream();
            await fileTransfer.SaveRulesAsync(stream, cancellationToken);
            uploaded.Add(FileTransferService.RulesName);
        }

        if (archive is { Length: > 0 })
        {
            await using var stream = archive.OpenReadStream();
            await fileTransfer.SaveArchiveAsync(stream, cancellationToken);
            uploaded.Add(FileTransferService.ArchiveName);
        }

        TempData["Message"] = $"Uploaded {string.Join(" and ", uploaded)}. Review the preview below before applying.";
        return RedirectToAction(nameof(FileTransfer));
    }

    /// <summary>
    /// Whether the caller is script that wants the state back, as opposed to a browser following a
    /// form post.
    /// </summary>
    private bool WantsJson =>
        Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    private static UploadedFileViewModel? ToViewModel(System.IO.FileInfo? info) =>
        info is null ? null : new UploadedFileViewModel(info.Name, info.Length, info.LastWriteTimeUtc);

    private string GetSafeReturnUrl(string? returnTo) =>
        returnTo is { Length: > 0 } && Url.IsLocalUrl(returnTo) ? returnTo : "/Admin";
}
