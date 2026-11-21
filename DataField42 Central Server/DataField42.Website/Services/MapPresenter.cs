using System.Text.Json;
using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using DataField42.Website.Models;
using Microsoft.EntityFrameworkCore;

namespace DataField42.Website.Services;

/// <summary>
/// Turns the analysis results stored on a <see cref="Map"/> into what the pages display: the
/// readme, the tags, the minimap markers and the list of what the level's spawners produce.
/// </summary>
public sealed class MapPresenter(DataField42DbContext context, FileDatabase fileDatabase, ILogger<MapPresenter> logger)
{
    /// <summary>
    /// The maps-list banner for one map.
    /// </summary>
    public MapBannerViewModel CreateBanner(Map map, IReadOnlyList<MapTagViewModel> tags, bool isAdmin)
    {
        string? modFolderName = map.Mod?.FolderName;
        string? readmeText = modFolderName is null ? null : fileDatabase.TryReadReadme(map, modFolderName, isAdmin);

        return new MapBannerViewModel
        {
            MapId = map.Id,
            Name = map.Name ?? map.NameRaw ?? string.Empty,
            AuthorName = map.Author?.Name,
            ModFolderName = modFolderName,
            ModFullName = map.Mod?.FullName,
            InGameMapUrl = fileDatabase.GetInGameMapUrl(map.Id),
            Tags = tags,
            MultiplayerBriefingObjectives = CleanBriefingObjectives(map.MultiplayerBriefingObjectives),
            ReadmeText = readmeText,
            HasConquest = map.HasConquest,
            HasCaptureTheFlag = map.HasCaptureTheFlag,
            HasTeamDeathmatch = map.HasTeamDeathmatch,
            HasCoop = map.HasCoop,
            HasObjectiveMode = map.HasObjectiveMode,
            DateText = FormatDateRange(map.DateOldestRfa, map.DateNewestRfa),
            ShowHasNoReadme = isAdmin && map.Readme is null,
        };
    }

    /// <summary>
    /// The tags of several maps at once, so a list of maps costs one query rather than one per
    /// map.
    /// </summary>
    public async Task<Dictionary<int, List<MapTagViewModel>>> GetTagsAsync(IReadOnlyCollection<int> mapIds, CancellationToken cancellationToken = default)
    {
        if (mapIds.Count == 0)
            return [];

        var links = await context.TagLinks
            .AsNoTracking()
            .Where(link => link.MapId != null && mapIds.Contains(link.MapId.Value) && link.Tag != null)
            .Select(link => new { MapId = link.MapId!.Value, link.Tag!.Name, link.Tag.Color, link.Tag.ColorHover })
            .ToListAsync(cancellationToken);

        return links
            .GroupBy(link => link.MapId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => new MapTagViewModel(link.Name ?? string.Empty, link.Color, link.ColorHover)).ToList());
    }

    /// <summary>
    /// The control-point markers, positioned as percentages of the minimap. Returns no markers
    /// when the level declared no combat area, since there is then no frame to place them in.
    /// </summary>
    public (IReadOnlyList<ControlPointViewModel> ControlPoints, int? WorldHeight) ParseControlPoints(Map map)
    {
        if (map.ControlPointsInfo is null)
            return ([], null);

        try
        {
            using var document = JsonDocument.Parse(map.ControlPointsInfo);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 2)
                return ([], null);

            var area = root[0];
            if (area.ValueKind != JsonValueKind.Array || area.GetArrayLength() < 4)
                return ([], null);

            double minX = area[0].GetDouble();
            double minZ = area[1].GetDouble();
            double width = area[2].GetDouble();
            double depth = area[3].GetDouble();
            if (width == 0 || depth == 0)
                return ([], null);

            double maxZ = minZ + depth;

            var controlPoints = new List<ControlPointViewModel>();
            int index = 0;
            foreach (var element in root[1].EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 4)
                    continue;

                var position = element[3];
                if (position.ValueKind != JsonValueKind.Array || position.GetArrayLength() < 3)
                    continue;

                double x = position[0].GetDouble();
                double z = position[2].GetDouble();

                controlPoints.Add(new ControlPointViewModel(
                    Id: index++,
                    Name: (element[0].GetString() ?? string.Empty).Replace('_', ' '),
                    Team: ReadStringOrNumber(element[1]),
                    Uncapturable: ReadStringOrNumber(element[2]) == "1",
                    RelativeLeft: (x - minX) / width * 100,
                    RelativeTop: (maxZ - z) / depth * 100));
            }

            return (controlPoints, (int)depth);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Map {MapId} has unreadable control point data", map.Id);
            return ([], null);
        }
    }

    /// <summary>
    /// What the level's spawners can produce, as a sorted distinct list. A spawner tied to a
    /// control point (one with a group id) can produce a different vehicle for each team, so both
    /// are listed; one without belongs to a single team.
    /// </summary>
    public IReadOnlyList<string> ParseObjectSpawns(Map map)
    {
        if (map.ObjectSpawnsInfo is null)
            return [];

        try
        {
            using var document = JsonDocument.Parse(map.ObjectSpawnsInfo);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var spawns = new List<string>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 5)
                    continue;

                var templatesByTeam = element[4];
                if (templatesByTeam.ValueKind != JsonValueKind.Object)
                    continue;

                bool tiedToControlPoint = element[1].ValueKind is not JsonValueKind.Null;
                if (tiedToControlPoint)
                {
                    foreach (var team in new[] { "1", "2" })
                        if (templatesByTeam.TryGetProperty(team, out var template) && template.GetString() is { } name)
                            spawns.Add(name);
                }
                else if (ReadStringOrNumber(element[2]) is { } ownTeam
                    && templatesByTeam.TryGetProperty(ownTeam, out var ownTemplate)
                    && ownTemplate.GetString() is { } ownName)
                {
                    spawns.Add(ownName);
                }
            }

            return [.. spawns.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Map {MapId} has unreadable object spawn data", map.Id);
            return [];
        }
    }

    /// <summary>
    /// The screenshot URLs for a map, which are served through the image route because they sit
    /// outside the web root.
    /// </summary>
    public IReadOnlyList<string> ParseImageUrls(Map map)
    {
        if (map.Images is null || map.Mod?.FolderName is null || map.Folder is null)
            return [];

        try
        {
            var names = JsonSerializer.Deserialize<List<string>>(map.Images) ?? [];
            return [.. names.Select(name => fileDatabase.GetMapImageUrl(map.Mod.FolderName, map.Folder, name))];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Map {MapId} has unreadable image data", map.Id);
            return [];
        }
    }

    /// <summary>
    /// The BattleCraft project paths of a map's archives, split into the mod and map folder the
    /// author kept the level under. The path is a Windows path from the author's own machine, so
    /// the interesting parts are counted from the end.
    /// </summary>
    public IReadOnlyList<BattleCraftPathViewModel> GetBattleCraftPaths(IEnumerable<Rfa> rfas)
    {
        var paths = new List<BattleCraftPathViewModel>();

        foreach (var rfa in rfas)
        {
            if (rfa.BattleCraftLevelPath is null)
                continue;

            var segments = rfa.BattleCraftLevelPath.Replace('\\', '/').Split('/').Reverse().ToList();
            paths.Add(new BattleCraftPathViewModel(
                rfa.BattleCraftLevelPath,
                ModFolder: segments.Count >= 6 ? segments[5] : null,
                MapFolder: segments.Count >= 3 ? segments[2] : null));
        }

        return paths;
    }

    /// <summary>
    /// The briefing text, unless it is the placeholder the level editor writes or is blank -- in
    /// which case the page falls back to the readme.
    /// </summary>
    public static string? CleanBriefingObjectives(string? text)
    {
        if (text is null || text.Trim().Length == 0)
            return null;

        if (text is "Edit the 'menu/init.con' and put your level destription here."
            or @"Edit the 'menu\init.con' and put your level destription here.")
            return null;

        return text;
    }

    /// <summary>
    /// A map's date: one day, or a range when its archives were built on different days.
    /// </summary>
    public static string? FormatDateRange(DateTime? oldest, DateTime? newest)
    {
        if (oldest is null)
            return null;

        return oldest == newest
            ? oldest.Value.ToString("yyyy-MM-dd")
            : $"{oldest.Value:yyyy-MM-dd} <-> {newest:yyyy-MM-dd}";
    }

    /// <summary>
    /// Team and flag values were written as either a string or a number depending on which
    /// version of the analysis produced them.
    /// </summary>
    private static string? ReadStringOrNumber(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.ToString(),
        JsonValueKind.True => "1",
        JsonValueKind.False => "0",
        _ => null,
    };
}
