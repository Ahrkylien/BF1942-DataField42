using System.Text.Json;
using DataField42.Website.Configuration;
using DataField42.Website.Data;
using DataField42.Website.Models;
using DataField42.Website.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Controllers;

/// <summary>
/// The working lists used while collecting maps: what has been found on the sites that host them,
/// what is still waiting to be sorted, and which maps a private server has seen played. All three
/// read files produced by other tools and describe work in progress, so they are admin-only.
/// </summary>
[Authorize(Policy = AdminAuthentication.PolicyName)]
public sealed class WorkListsController(
    DataField42DbContext context,
    IOptions<WebsiteOptions> options,
    ILogger<WorkListsController> logger) : Controller
{
    /// <summary>
    /// The mod the scraped listings are compared against; every site scraped hosts maps for the
    /// base game.
    /// </summary>
    private const string ScrapedMod = "BF1942";

    /// <summary>
    /// Folder status to the colour the page marks it with. Only white folders -- the ones nothing
    /// has been decided about -- are listed.
    /// </summary>
    private static readonly string[] FolderColors = ["white", "blue", "#fff600", "#ff8f00", "#ff0505"];

    /// <summary>
    /// How much later a copy's timestamp may be before it is treated as a different version
    /// rather than the same file.
    /// </summary>
    private static readonly TimeSpan DateTolerance = TimeSpan.FromHours(3);

    private readonly WebsiteOptions _options = options.Value;

    [HttpGet("/Scraping")]
    public async Task<IActionResult> Scraping(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Scraping";

        if (_options.ScrapedPagesFile is not { Length: > 0 } path || !System.IO.File.Exists(path))
            return View(new ScrapingPageViewModel());

        List<ScrapedPage>? scraped;
        try
        {
            await using var stream = System.IO.File.OpenRead(path);
            scraped = await JsonSerializer.DeserializeAsync<List<ScrapedPage>>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "Could not read scraped pages from {Path}", path);
            return View(new ScrapingPageViewModel());
        }

        var pages = (scraped ?? [])
            .Select(page => new
            {
                Page = page,
                Comparable = StripFileNameDecoration(page.Title),
                ArchiveName = GetArchiveName(page.FileName),
            })
            .OrderBy(page => page.ArchiveName ?? page.Comparable, StringComparer.OrdinalIgnoreCase)
            .ThenBy(page => SourceOrder(page.Page.Source))
            .ToList();

        var mapNames = await context.Maps
            .AsNoTracking()
            .Where(map => map.Mod!.FolderName == ScrapedMod && map.Name != null)
            .Select(map => new { map.Name, HasReadme = map.Readme != null })
            .ToListAsync(cancellationToken);

        var maps = mapNames
            .GroupBy(map => map.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Any(map => map.HasReadme), StringComparer.OrdinalIgnoreCase);

        var result = new List<ScrapedPageViewModel>(pages.Count);
        string? previousComparable = null;
        int missing = 0;

        foreach (var page in pages)
        {
            var model = ToViewModel(page.Page);

            if (string.Equals(page.Comparable, previousComparable, StringComparison.OrdinalIgnoreCase))
            {
                model.Status = ScrapedPageStatus.Duplicate;
            }
            else if (FindMap(maps, page.Comparable, page.ArchiveName) is { } hasReadme)
            {
                model.Status = hasReadme ? ScrapedPageStatus.Complete : ScrapedPageStatus.InCollectionWithoutReadme;
            }
            else
            {
                model.Status = ScrapedPageStatus.Missing;
                missing++;
            }

            previousComparable = page.Comparable;
            result.Add(model);
        }

        return View(new ScrapingPageViewModel { Pages = result, MissingCount = missing });
    }

    [HttpGet("/ToDoFiles")]
    public async Task<IActionResult> ToDoFiles(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "To Do Files";

        if (_options.FolderInfosFile is not { Length: > 0 } path || !System.IO.File.Exists(path))
            return View(new ToDoFilesPageViewModel());

        JsonElement root;
        try
        {
            await using var stream = System.IO.File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            root = document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "Could not read folder information from {Path}", path);
            return View(new ToDoFilesPageViewModel());
        }

        var folders = new List<ToDoFolderViewModel>();
        foreach (var folderElement in root.EnumerateArray())
        {
            if (folderElement.ValueKind != JsonValueKind.Array || folderElement.GetArrayLength() < 4)
                continue;

            int status = folderElement[3].TryGetInt32(out int parsed) ? parsed : -1;

            // Only folders nothing has been decided about still need work.
            if (status != 0)
                continue;

            string color = FolderColors[status];
            folders.Add(new ToDoFolderViewModel(
                folderElement[0].GetString() ?? string.Empty,
                color,
                ReadFiles(folderElement[1], color)));
        }

        return View(new ToDoFilesPageViewModel { Folders = folders });
    }

    [HttpGet("/MyServer")]
    public async Task<IActionResult> MyServer(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "My Server";

        if (_options.PlayerActivitiesFile is not { Length: > 0 } path || !System.IO.File.Exists(path))
            return View(new MyServerPageViewModel());

        Dictionary<string, List<List<string>>>? activities;
        try
        {
            await using var stream = System.IO.File.OpenRead(path);
            activities = await JsonSerializer.DeserializeAsync<Dictionary<string, List<List<string>>>>(stream, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "Could not read player activities from {Path}", path);
            return View(new MyServerPageViewModel());
        }

        var groups = (activities ?? [])
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (
                GroupKey: group.Key,
                Maps: (IReadOnlyList<string>)group.Value
                    .Where(entry => entry.Count >= 3)
                    .Select(entry => $"{entry[0]} {entry[1]} {entry[2]}")
                    .ToList()))
            .ToList();

        return View(new MyServerPageViewModel { MapGroups = groups });
    }

    private static List<ToDoFileViewModel> ReadFiles(JsonElement filesElement, string folderColor)
    {
        var files = new List<ToDoFileViewModel>();
        if (filesElement.ValueKind != JsonValueKind.Array)
            return files;

        foreach (var fileElement in filesElement.EnumerateArray())
        {
            if (fileElement.ValueKind != JsonValueKind.Array || fileElement.GetArrayLength() < 6)
                continue;

            double modified = fileElement[3].GetDouble();
            files.Add(new ToDoFileViewModel(
                fileElement[0].GetString() ?? string.Empty,
                fileElement[1].GetString(),
                fileElement[2].GetInt64(),
                FromUnixSeconds(modified),
                ReadLinkedFiles(fileElement[5], modified)));
        }

        return files;
    }

    /// <summary>
    /// The copies of a file found elsewhere, coloured by age: a copy noticeably newer than this
    /// one is marked red, because it may be the version that should be kept; a noticeably older
    /// one is marked green.
    /// </summary>
    private static List<LinkedFileViewModel> ReadLinkedFiles(JsonElement linkedElement, double modified)
    {
        var linked = new List<LinkedFileViewModel>();
        if (linkedElement.ValueKind != JsonValueKind.Array)
            return linked;

        double tolerance = DateTolerance.TotalSeconds;

        foreach (var element in linkedElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 4)
                continue;

            double otherModified = element[3].GetDouble();
            string color = otherModified > modified + tolerance
                ? "#ff0505"
                : otherModified < modified - tolerance ? "green" : "";

            linked.Add(new LinkedFileViewModel(
                element[0].GetString() ?? string.Empty,
                element[1].GetString(),
                element[2].GetInt64(),
                FromUnixSeconds(otherModified),
                color));
        }

        return linked;
    }

    private static DateTime FromUnixSeconds(double seconds) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000)).LocalDateTime;

    private static ScrapedPageViewModel ToViewModel(ScrapedPage page) => new()
    {
        Source = page.Source,
        Title = page.Title,
        FileName = page.FileName,
        Link1 = page.Link1,
        Link2 = page.Link2,
        Link3 = page.Link3,
        Date = page.Date,
        FileSize = page.FileSize,
        FileSizeOld = page.FileSizeOld,
        Downloads = page.Downloads,
        OtherSource = page.OtherSource,
    };

    /// <summary>
    /// Whether a scraped listing matches a map in the collection, and if so whether that map has
    /// a readme. Listings are matched on their title and, for a bare archive, on the level name
    /// inside the file name.
    /// </summary>
    private static bool? FindMap(Dictionary<string, bool> maps, string title, string? archiveName)
    {
        if (maps.TryGetValue(title, out bool hasReadme))
            return hasReadme;
        if (archiveName is not null && maps.TryGetValue(archiveName, out hasReadme))
            return hasReadme;

        return null;
    }

    /// <summary>
    /// A listing's title without the archive extension it usually ends in, and with underscores
    /// read as spaces -- which is how map names are stored.
    /// </summary>
    private static string StripFileNameDecoration(string? title)
    {
        string result = (title ?? string.Empty).Trim();

        foreach (var suffix in new[] { ".zip", ".rar", ".rfa", ".exe" })
            if (result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                result = result[..^suffix.Length];
                break;
            }

        return result.Replace('_', ' ');
    }

    /// <summary>
    /// The level name inside a bare <c>.rfa</c> listing's file name, which is more reliable than
    /// its title.
    /// </summary>
    private static string? GetArchiveName(string? fileName)
    {
        if (fileName is null || !fileName.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
            return null;

        return Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ');
    }

    /// <summary>
    /// Which site a listing came from decides the order within one map's listings.
    /// </summary>
    private static int SourceOrder(string? source) => source switch
    {
        "L" => 0,
        "M" => 1,
        "A" => 2,
        _ => 3,
    };

    /// <summary>
    /// A listing as the scraper writes it.
    /// </summary>
    private sealed record ScrapedPage
    {
        public string? Source { get; init; }

        public string? Title { get; init; }

        public string? FileName { get; init; }

        public string? Link1 { get; init; }

        public string? Link2 { get; init; }

        public string? Link3 { get; init; }

        public string? Date { get; init; }

        public string? FileSize { get; init; }

        public string? FileSizeOld { get; init; }

        public string? Downloads { get; init; }

        public string? OtherSource { get; init; }
    }
}
