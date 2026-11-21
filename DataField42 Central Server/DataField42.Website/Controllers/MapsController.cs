using System.Text.Json;
using DataField42.Website.Configuration;
using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using DataField42.Website.Models;
using DataField42.Website.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Controllers;

/// <summary>
/// The mods list, the maps list and the page for one map.
/// </summary>
public sealed class MapsController(
    DataField42DbContext context,
    MapPresenter presenter,
    FileDatabase fileDatabase,
    IOptions<WebsiteOptions> options) : Controller
{
    private const int MapsPerPage = 10;

    /// <summary>
    /// How many page links to show around the current page.
    /// </summary>
    private const int VisiblePageNumbers = 5;

    private readonly WebsiteOptions _options = options.Value;

    private bool IsAdmin => User.Identity?.IsAuthenticated == true;

    [HttpGet("/Mods")]
    public async Task<IActionResult> Mods(CancellationToken cancellationToken)
    {
        var mods = await context.Mods
            .AsNoTracking()
            .OrderBy(mod => mod.FolderName)
            .Select(mod => new
            {
                mod.FolderName,
                mod.FullName,
                mod.Version,
                mod.LastUpdate,
                mod.ModDependencies,
                MapCount = mod.Maps.Count,
            })
            .ToListAsync(cancellationToken);

        var model = mods
            .Where(mod => mod.MapCount > 0 && mod.FolderName is not null)
            .Select(mod => new ModViewModel
            {
                FolderName = mod.FolderName!,
                FullName = mod.FullName,
                Version = mod.Version,
                LastUpdate = mod.LastUpdate,
                MapCount = mod.MapCount,
                Dependencies = ParseDependencies(mod.ModDependencies, mod.FolderName!),
            })
            .ToList();

        ViewData["Title"] = "Mods";
        ViewData["ActiveNav"] = "Mods";
        return View(model);
    }

    [HttpGet("/Maps")]
    public async Task<IActionResult> Maps(
        [FromQuery] string? mod,
        [FromQuery] string? map,
        [FromQuery] string? author,
        [FromQuery] string? gpm,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var gameplayModes = GameplayModeFilter.Parse(gpm);
        page = Math.Max(1, page);

        var selectedMod =
            await context.Mods.AsNoTracking().FirstOrDefaultAsync(m => m.FolderName == (mod ?? _options.DefaultMod), cancellationToken)
            ?? await context.Mods.AsNoTracking().FirstOrDefaultAsync(m => m.FolderName == _options.DefaultMod, cancellationToken);

        if (selectedMod?.FolderName is null)
            return View(EmptyMapsPage(gameplayModes, page));

        var query = BuildMapQuery(selectedMod.Id, map, author, gameplayModes);

        int mapCount = await query.CountAsync(cancellationToken);
        int pageCount = Math.Max(1, (int)Math.Ceiling(mapCount / (double)MapsPerPage));
        page = Math.Min(page, pageCount);

        var maps = await query
            .OrderByDescending(m => m.Id)
            .Skip((page - 1) * MapsPerPage)
            .Take(MapsPerPage)
            .Include(m => m.Mod)
            .Include(m => m.Author)
            .ToListAsync(cancellationToken);

        var tags = await presenter.GetTagsAsync([.. maps.Select(m => m.Id)], cancellationToken);

        var model = new MapsPageViewModel
        {
            SelectedModFolderName = selectedMod.FolderName,
            MapNameFilter = map ?? string.Empty,
            AuthorFilter = author ?? string.Empty,
            GameplayModes = gameplayModes,
            ActivePage = page,
            PageNumbers = BuildPageNumbers(page, pageCount),
            Mods = await context.Mods.AsNoTracking()
                .Where(m => m.FolderName != null)
                .OrderBy(m => m.FolderName)
                .Select(m => new ValueTuple<string, string?>(m.FolderName!, m.FullName))
                .ToListAsync(cancellationToken),
            Authors = await context.Authors.AsNoTracking()
                .Where(a => a.Name != null)
                .OrderBy(a => a.Name)
                .Select(a => a.Name!)
                .ToListAsync(cancellationToken),
            Maps = [.. maps.Select(m => presenter.CreateBanner(m, tags.GetValueOrDefault(m.Id, []), IsAdmin))],
        };

        ViewData["Title"] = "Maps";
        ViewData["ActiveNav"] = "Maps";
        return View(model);
    }

    [HttpGet("/Map/{id:int}")]
    public async Task<IActionResult> Map(int id, CancellationToken cancellationToken)
    {
        var map = await context.Maps
            .AsNoTracking()
            .Include(m => m.Mod)
            .Include(m => m.Author)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        ViewData["Title"] = "Map";

        if (map is null)
            return View((MapDetailsViewModel?)null);

        var rfas = await context.Rfas
            .AsNoTracking()
            .Where(rfa => rfa.MapId == id)
            .OrderBy(rfa => rfa.PatchNumber)
            .ToListAsync(cancellationToken);

        var otherMods = await context.Maps
            .AsNoTracking()
            .Include(m => m.Mod)
            .Where(m => m.Id != id && m.NameRaw == map.NameRaw)
            .Select(m => new ValueTuple<int, string?>(m.Id, m.Mod!.FullName))
            .ToListAsync(cancellationToken);

        var tags = await presenter.GetTagsAsync([id], cancellationToken);
        var (controlPoints, worldHeight) = presenter.ParseControlPoints(map);

        var model = new MapDetailsViewModel
        {
            MapId = map.Id,
            Name = map.Name ?? map.NameRaw ?? string.Empty,
            AuthorName = map.Author?.Name,
            ModFolderName = map.Mod?.FolderName,
            ModFullName = map.Mod?.FullName,
            GameMapId = map.GameMapId,
            InGameMapUrl = fileDatabase.GetInGameMapUrl(map.Id),
            WorldHeight = worldHeight,
            ControlPoints = controlPoints,
            ObjectSpawns = presenter.ParseObjectSpawns(map),
            Tags = tags.GetValueOrDefault(id, []),
            Rfas = [.. rfas.Select(rfa => new MapRfaViewModel(rfa.Id, rfa.FileName, rfa.Date))],
            ImageUrls = presenter.ParseImageUrls(map),
            BattleCraftPaths = presenter.GetBattleCraftPaths(rfas),
            MapsFromOtherMods = otherMods,
            MultiplayerBriefingObjectives = MapPresenter.CleanBriefingObjectives(map.MultiplayerBriefingObjectives),
            ReadmeText = map.Mod?.FolderName is { } folderName
                ? fileDatabase.TryReadReadme(map, folderName, IsAdmin)
                : null,
            HasConquest = map.HasConquest,
            HasCaptureTheFlag = map.HasCaptureTheFlag,
            HasTeamDeathmatch = map.HasTeamDeathmatch,
            HasCoop = map.HasCoop,
            HasObjectiveMode = map.HasObjectiveMode,
            IsAdmin = IsAdmin,
        };

        return View(model);
    }

    [HttpGet("/Map/{id:int}/Download")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var map = await context.Maps
            .AsNoTracking()
            .Include(m => m.Mod)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (map?.Mod?.FolderName is not { } modFolderName)
            return Redirect("/");

        var rfas = await context.Rfas
            .AsNoTracking()
            .Where(rfa => rfa.MapId == id)
            .OrderBy(rfa => rfa.PatchNumber)
            .ToListAsync(cancellationToken);

        var archive = await fileDatabase.CreateMapArchiveAsync(modFolderName, rfas, cancellationToken);
        return File(archive, "application/zip", $"{map.Name} ({modFolderName}).zip");
    }

    /// <summary>
    /// Serves one of a map's screenshots out of the mods directory, which is not inside the web
    /// root.
    /// </summary>
    [HttpGet("/MapImg/{hexPath}")]
    public IActionResult MapImage(string hexPath)
    {
        string? path = fileDatabase.ResolveMapImagePath(hexPath);
        if (path is null)
            return NotFound();

        return PhysicalFile(path, GetImageContentType(path));
    }

    private IQueryable<Map> BuildMapQuery(int modId, string? mapName, string? author, GameplayModeFilter gameplayModes)
    {
        var query = context.Maps.AsNoTracking().Where(m => m.ModId == modId);

        if (!string.IsNullOrEmpty(mapName))
        {
            // Map names are stored with spaces, but are linked to with underscores.
            string pattern = $"%{mapName.Replace('_', ' ')}%";
            query = query.Where(m => EF.Functions.Like(m.Name!, pattern));
        }

        if (!string.IsNullOrEmpty(author))
            query = query.Where(m => m.Author != null && m.Author.Name == author);

        query = query.Where(m =>
            (gameplayModes.Conquest && m.GpmConquest == 1) ||
            (gameplayModes.CaptureTheFlag && m.GpmCaptureTheFlag == 1) ||
            (gameplayModes.TeamDeathmatch && m.GpmTeamDeathmatch == 1) ||
            (gameplayModes.Coop && m.GpmCoop == 1) ||
            (gameplayModes.ObjectiveMode && m.GpmObjectiveMode == 1));

        return query;
    }

    private MapsPageViewModel EmptyMapsPage(GameplayModeFilter gameplayModes, int page) => new()
    {
        SelectedModFolderName = _options.DefaultMod,
        MapNameFilter = string.Empty,
        AuthorFilter = string.Empty,
        GameplayModes = gameplayModes,
        ActivePage = page,
    };

    /// <summary>
    /// The page links: a run around the current page, always including the first and last page,
    /// with a zero standing in for the gap between runs.
    /// </summary>
    private static List<int> BuildPageNumbers(int activePage, int pageCount)
    {
        int half = VisiblePageNumbers / 2;
        var pages = new List<int>();

        // The first and last page spend half their window on pages that do not exist, so they
        // get the other half back on the side that does.
        int extra = activePage == 1 || activePage == pageCount ? half : 0;
        int offset = activePage == pageCount ? half : 0;

        for (int i = 0; i < VisiblePageNumbers + extra; i++)
        {
            int page = i - half + activePage - offset;
            if (page >= 1 && page <= pageCount)
                pages.Add(page);
        }

        if (!pages.Contains(1))
        {
            pages.Insert(0, 1);
            if (pages.Count > 1 && pages[1] != 2)
                pages.Insert(1, 0);
        }

        if (!pages.Contains(pageCount))
        {
            if (pages.Count > 0 && pages[^1] != pageCount - 1)
                pages.Add(0);
            pages.Add(pageCount);
        }

        return pages;
    }

    /// <summary>
    /// The mods a mod needs, by folder name. A mod listing itself is skipped, which several do.
    /// </summary>
    private static List<string> ParseDependencies(string? modDependencies, string folderName)
    {
        if (modDependencies is null)
            return [];

        try
        {
            var paths = JsonSerializer.Deserialize<List<string>>(modDependencies) ?? [];
            return
            [
                .. paths
                    .Select(path => path.Replace('\\', '/').TrimEnd('/').Split('/').Last())
                    .Where(name => name.Length > 0 && !name.Equals(folderName, StringComparison.OrdinalIgnoreCase))
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string GetImageContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".dds" => "image/vnd-ms.dds",
        ".tga" => "image/x-tga",
        _ => "application/octet-stream",
    };
}
