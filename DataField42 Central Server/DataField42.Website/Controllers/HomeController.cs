using DataField42.Website.Models;
using DataField42.Website.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataField42.Website.Controllers;

/// <summary>
/// The front page and the small pages that need no data of their own.
/// </summary>
public sealed class HomeController(
    ServerListService serverList,
    PlayerActivityStatistics statistics,
    ReferencePageStore referencePages) : Controller
{
    [HttpGet("/")]
    [HttpGet("/Home")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var servers = await serverList.GetServersAsync(cancellationToken);
        var populated = servers.Where(server => server.PlayerCount > 0).ToList();

        var model = new HomePageViewModel
        {
            TotalPlayers = populated.Sum(server => server.PlayerCount),
            Servers = populated,
            PlayerNumbers = new LineGraphViewModel
            {
                Samples = await statistics.GetTotalSeriesAsync(TimeSpan.FromDays(1), cancellationToken),
            },
        };

        ViewData["Title"] = "Home";
        ViewData["ActiveNav"] = "Home";
        ViewData["HasChartJs"] = true;
        return View(model);
    }

    [HttpGet("/AllCMD")]
    public IActionResult AllCommands() => ReferencePage("AllCommands", "All Commands");

    [HttpGet("/ObjectTemplate")]
    public IActionResult ObjectTemplate() => ReferencePage("ObjectTemplate", "ObjectTemplate Commands");

    [HttpGet("/GeometryTemplate")]
    public IActionResult GeometryTemplate() => ReferencePage("GeometryTemplate", "GeometryTemplate Commands");

    [HttpGet("/GC_Errors")]
    public IActionResult GcErrors() => ReferencePage("GcErrors", "GC Errors");

    /// <summary>
    /// Renders one of the fixed reference documents inside the site layout.
    /// </summary>
    private IActionResult ReferencePage(string name, string title)
    {
        if (referencePages.Read(name) is not { } html)
            return NotFound();

        ViewData["Title"] = title;
        return View("Reference", html);
    }

    // Any verb: an error on a POST is re-executed here with the original method, and a GET-only
    // endpoint would answer that with 405, hiding what actually went wrong.
    [Route("/Error")]
    public IActionResult Error()
    {
        ViewData["Title"] = "Error";
        return View();
    }

    /// <summary>
    /// A request for a page that does not exist goes to the front page, which is what a visitor
    /// following an old link wants. A request for a missing file keeps its status code, so a
    /// broken image stays broken rather than returning a page, and so does anything that is not a
    /// plain GET -- a failed form post is an error to report, not a link to redirect.
    /// </summary>
    [Route("/NotFound")]
    public IActionResult NotFoundPage([FromQuery] int code = 404)
    {
        string? originalPath = HttpContext.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>()?.OriginalPath;

        if (!HttpMethods.IsGet(Request.Method))
            return StatusCode(code);

        if (originalPath is not null && Path.HasExtension(originalPath))
            return StatusCode(code);

        return Redirect("/");
    }
}
