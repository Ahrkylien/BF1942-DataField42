using DataField42.Website.Models;
using DataField42.Website.Security;
using DataField42.Website.Services.Admin.Update;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataField42.Website.Controllers;

/// <summary>
/// The Update page: deploys a branch of the repository on GitHub to this server.
/// </summary>
[Authorize(Policy = AdminAuthentication.PolicyName)]
[Route("/Admin/Update")]
public sealed class AdminUpdateController(UpdateService updates) : Controller
{
    private const string DefaultBranch = "main";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Update";
        ViewData["ActiveNav"] = "Admin";

        var branches = updates.IsAvailable ? await updates.GetBranchesAsync(cancellationToken) : [];
        var status = updates.GetStatus();

        string selected = branches.Contains(DefaultBranch) ? DefaultBranch
            : status.Deployed?.Branch is { } deployedBranch && branches.Contains(deployedBranch) ? deployedBranch
            : branches.FirstOrDefault() ?? DefaultBranch;

        return View(new UpdatePageViewModel
        {
            IsAvailable = updates.IsAvailable,
            Branches = branches,
            SelectedBranch = selected,
            Status = status,
        });
    }

    /// <summary>
    /// Fetches from GitHub and answers with what the branch holds that the running site does not.
    /// </summary>
    [HttpPost("Check")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Check([FromForm] string? branch, CancellationToken cancellationToken)
    {
        if (!updates.IsAvailable)
            return Conflict(new { error = "This installation cannot update itself." });

        try
        {
            return Json(await updates.CheckAsync(branch ?? DefaultBranch, cancellationToken));
        }
        catch (UpdateException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Starts the update. The answer is the state right after starting; the page then follows
    /// <see cref="Status"/>, which keeps answering across the restart the update ends with.
    /// </summary>
    [HttpPost("Start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start([FromForm] string? branch, CancellationToken cancellationToken)
    {
        if (!updates.IsAvailable)
            return Conflict(new { error = "This installation cannot update itself." });

        try
        {
            await updates.StartAsync(branch ?? DefaultBranch, cancellationToken);
            return Json(updates.GetStatus());
        }
        catch (UpdateException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("Status")]
    public IActionResult Status() =>
        Json(updates.GetStatus());
}
