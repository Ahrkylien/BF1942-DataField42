using DataField42.Website.Configuration;
using DataField42.Website.Data;
using DataField42.Website.Models;
using DataField42.Website.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Controllers;

/// <summary>
/// The pages that report on the state of the collection rather than on the game: storage use,
/// integrity findings, and the maps still to be collected.
/// </summary>
public sealed class DiagnosticsController(
    DataField42DbContext context,
    DatabaseIntegrityService integrity,
    DiskSpaceReader diskSpace,
    PlayerActivityStatistics statistics,
    IOptions<WebsiteOptions> options) : Controller
{
    private readonly WebsiteOptions _options = options.Value;

    [HttpGet("/Extra")]
    public async Task<IActionResult> Extra(CancellationToken cancellationToken)
    {
        var model = new ExtraPageViewModel
        {
            Filesystems = await diskSpace.ReadAsync(_options.MonitoredFilesystems, cancellationToken),
            PlayerNumbers = _options.FeaturedServerId is { } serverId
                ? new LineGraphViewModel { Samples = await statistics.GetWeekAverageAsync(serverId, cancellationToken: cancellationToken) }
                : null,
        };

        ViewData["Title"] = "Extra";
        ViewData["HasChartJs"] = true;
        return View(model);
    }

    [HttpGet("/ErrorList")]
    public async Task<IActionResult> ErrorList(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Error List";
        return View(new ErrorListPageViewModel { Groups = await integrity.CheckAsync(cancellationToken) });
    }

    /// <summary>
    /// Maps that servers have been seen playing which the file database does not hold. For the
    /// base game the servers they were seen on are linked, so the map can be asked for.
    /// </summary>
    [HttpGet("/MissingMaps")]
    public async Task<IActionResult> MissingMaps(CancellationToken cancellationToken)
    {
        var missing = await context.MapsNotInDatabase.AsNoTracking().ToListAsync(cancellationToken);

        var serversByMap = (await context.Rounds
                .AsNoTracking()
                .Where(round => round.ModName == "bf1942" && round.MapName != null && round.ServerId != null)
                .Select(round => new { round.MapName, round.ServerId })
                .Distinct()
                .ToListAsync(cancellationToken))
            .GroupBy(round => round.MapName!.ToLowerInvariant())
            .ToDictionary(group => group.Key, group => group.Select(round => round.ServerId!.Value).ToList());

        var mapsPerMod = missing
            .Where(entry => entry.MapName is not null && entry.ModName is not null)
            .GroupBy(entry => entry.ModName!.ToLowerInvariant())
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (
                Mod: group.Key,
                Maps: (IReadOnlyList<MissingMapViewModel>)group
                    .OrderBy(entry => entry.MapName, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => new MissingMapViewModel(
                        entry.MapName!,
                        group.Key == "bf1942"
                            ? serversByMap.GetValueOrDefault(entry.MapName!.ToLowerInvariant(), [])
                            : []))
                    .ToList()))
            .ToList();

        ViewData["Title"] = "Missing Maps";
        return View(new MissingMapsPageViewModel { MapsPerMod = mapsPerMod });
    }
}
