using DataField42.Website.Data;
using DataField42.Website.Models;
using Microsoft.EntityFrameworkCore;
using YamlDotNet.Serialization;

namespace DataField42.Website.Services;

/// <summary>
/// Checks the file database for the inconsistencies that creep in as maps are added, repackaged
/// and copied between mods. Findings are reported, never fixed: every one of them needs a person
/// to decide what the right answer is.
/// </summary>
public sealed class DatabaseIntegrityService(DataField42DbContext context, IWebHostEnvironment environment, ILogger<DatabaseIntegrityService> logger)
{
    /// <summary>
    /// Runs every check.
    /// </summary>
    public async Task<List<IntegrityFindingGroup>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var exceptions = ReadExceptions();

        // What the analysis finds out about a map can only be judged for a map it has run for.
        var maps = await ReadMapsAsync(cancellationToken);
        var analysed = maps.Where(map => map.IsAnalysed).ToList();

        return
        [
            new("Double Maps in same Mod", await FindDuplicateMapsAsync(cancellationToken)),
            new("Maps without Mod", await FindMapsWithoutModAsync(cancellationToken)),
            new("Not analysed", [.. maps.Where(map => !map.IsAnalysed).Select(map => map.Line)]),
            new("Wrong game.mapID (not the same as the mod folderName)", FindWrongGameMapId(analysed, exceptions)),
            new("No game.mapID", FindMissingGameMapId(analysed, exceptions)),
            new("No gameplaymode at all", [.. analysed.Where(map => !map.HasGameplayMode).Select(map => map.Line)]),
            new("Double RFAs in Map", await FindDuplicateRfasAsync(cancellationToken)),
            new("Map without (base) RFA", await FindMapsWithoutBaseRfaAsync(cancellationToken)),
            new("RFA without Map", await FindOrphanedRfasAsync(cancellationToken)),
        ];
    }

    /// <summary>
    /// Two maps of the same name in one mod: one of them is a leftover from a repackaging.
    /// </summary>
    private async Task<List<string>> FindDuplicateMapsAsync(CancellationToken cancellationToken)
    {
        var duplicates = await context.Maps
            .AsNoTracking()
            .GroupBy(map => new { map.NameRaw, map.ModId })
            .Where(group => group.Count() > 1)
            .Select(group => new { group.Key.NameRaw, group.Key.ModId, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return [.. duplicates.Select(d => $"nameRaw: '{d.NameRaw}', modID: {d.ModId}, count: {d.Count}")];
    }

    private async Task<List<string>> FindMapsWithoutModAsync(CancellationToken cancellationToken)
    {
        var maps = await context.Maps
            .AsNoTracking()
            .Where(map => map.Mod == null)
            .Select(map => new { map.Id, map.Name, map.ModId })
            .ToListAsync(cancellationToken);

        return [.. maps.Select(m => $"ID: {m.Id}, name: '{m.Name}', modID: {m.ModId}")];
    }

    /// <summary>
    /// Every map with what the analysis recorded for it. The analysed maps are a few thousand, so the
    /// checks on them are made here and not in the database.
    /// </summary>
    private async Task<List<MapAnalysis>> ReadMapsAsync(CancellationToken cancellationToken) =>
        await context.Maps
            .AsNoTracking()
            .Select(map => new MapAnalysis(
                map.Id,
                map.Name,
                map.Mod != null ? map.Mod.FolderName : null,
                map.GameMapId,
                map.GpmConquest,
                map.GpmCaptureTheFlag,
                map.GpmTeamDeathmatch,
                map.GpmCoop,
                map.GpmObjectiveMode))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// A map whose own <c>init.con</c> names a different mod than the one it is filed under,
    /// which means it was copied between mods without being updated and will not load. A map that
    /// names no mod at all is reported as having no game.mapID instead.
    /// </summary>
    private static List<string> FindWrongGameMapId(List<MapAnalysis> maps, IntegrityExceptions exceptions) =>
    [
        .. maps
            .Where(map => map.ModFolderName is not null && map.GameMapId is not null
                && !string.Equals(map.ModFolderName, map.GameMapId, StringComparison.OrdinalIgnoreCase)
                && !exceptions.IsAllowed("WrongGameMapId", map.Name, map.ModFolderName))
            .OrderBy(map => map.ModFolderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(map => map.GameMapId, StringComparer.OrdinalIgnoreCase)
            .Select(map => $"ID: {map.Id}, name: '{map.Name}', gameMapId: '{map.GameMapId}', Mod folderName: {map.ModFolderName}")
    ];

    private static List<string> FindMissingGameMapId(List<MapAnalysis> maps, IntegrityExceptions exceptions) =>
    [
        .. maps
            .Where(map => map.GameMapId is null && !exceptions.IsAllowed("NoGameMapId", map.Name, map.ModFolderName))
            .Select(map => map.Line)
    ];

    /// <summary>
    /// Two archives claiming the same patch number of the same map: only one of them will load.
    /// </summary>
    private async Task<List<string>> FindDuplicateRfasAsync(CancellationToken cancellationToken)
    {
        var duplicates = await context.Rfas
            .AsNoTracking()
            .GroupBy(rfa => new { rfa.MapId, rfa.PatchNumber })
            .Where(group => group.Count() > 1)
            .Select(group => new
            {
                Id = group.Min(rfa => rfa.Id),
                Path = group.Min(rfa => rfa.Path),
                group.Key.MapId,
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        return [.. duplicates.Select(d => $"ID: {d.Id}, path: '{d.Path}', mapID: {d.MapId}, count: {d.Count}")];
    }

    /// <summary>
    /// A map with only patch archives and no base archive, which cannot be installed.
    /// </summary>
    private async Task<List<string>> FindMapsWithoutBaseRfaAsync(CancellationToken cancellationToken)
    {
        var maps = await context.Maps
            .AsNoTracking()
            .Where(map => !map.Rfas.Any(rfa => rfa.PatchNumber == null))
            .Select(map => new { map.Id, map.Name, map.ModId })
            .ToListAsync(cancellationToken);

        return [.. maps.Select(m => $"ID: {m.Id}, name: '{m.Name}', modID: {m.ModId}")];
    }

    private async Task<List<string>> FindOrphanedRfasAsync(CancellationToken cancellationToken)
    {
        var rfas = await context.Rfas
            .AsNoTracking()
            .Where(rfa => rfa.Map == null)
            .Select(rfa => new { rfa.Id, rfa.Path, rfa.MapId })
            .ToListAsync(cancellationToken);

        return [.. rfas.Select(r => $"ID: {r.Id}, mapID: '{r.MapId}', path: {r.Path}")];
    }

    /// <summary>
    /// The known-and-accepted findings, read from <c>Resources/integrityExceptions.yaml</c>, so
    /// the deliberate oddities stop being reported without the check itself being weakened.
    /// </summary>
    private IntegrityExceptions ReadExceptions()
    {
        string path = Path.Combine(environment.ContentRootPath, "Resources", "integrityExceptions.yaml");
        if (!File.Exists(path))
            return IntegrityExceptions.None;

        try
        {
            var parsed = new DeserializerBuilder().Build()
                .Deserialize<Dictionary<string, List<Dictionary<string, string>>>>(File.ReadAllText(path));
            return new IntegrityExceptions(parsed ?? []);
        }
        catch (Exception ex) when (ex is IOException or YamlDotNet.Core.YamlException)
        {
            logger.LogWarning(ex, "Could not read integrity exceptions from {Path}", path);
            return IntegrityExceptions.None;
        }
    }

    /// <summary>
    /// A map as far as the integrity checks care: what it is filed under, and what the analysis made of it.
    /// </summary>
    /// <remarks>
    /// The analysis fills in the game id and all five gameplay modes together, so a missing mode
    /// means it has not run for the map.
    /// </remarks>
    private sealed record MapAnalysis(
        int Id,
        string? Name,
        string? ModFolderName,
        string? GameMapId,
        int? Conquest,
        int? CaptureTheFlag,
        int? TeamDeathmatch,
        int? Coop,
        int? ObjectiveMode)
    {
        public bool IsAnalysed => Conquest is not null && CaptureTheFlag is not null && TeamDeathmatch is not null
            && Coop is not null && ObjectiveMode is not null;

        public bool HasGameplayMode => Conquest == 1 || CaptureTheFlag == 1 || TeamDeathmatch == 1 || Coop == 1 || ObjectiveMode == 1;

        public string Line => $"ID: {Id}, name: '{Name}' Mod folderName: '{ModFolderName}'";
    }

    /// <summary>
    /// Map/mod pairs that are deliberately the way they are, by check name.
    /// </summary>
    private sealed class IntegrityExceptions(Dictionary<string, List<Dictionary<string, string>>> entries)
    {
        public static IntegrityExceptions None { get; } = new([]);

        public bool IsAllowed(string check, string? mapName, string? modName)
        {
            if (!entries.TryGetValue(check, out var allowed))
                return false;

            foreach (var entry in allowed)
            {
                bool mapMatches = !entry.TryGetValue("Map", out var map) || string.Equals(map, mapName, StringComparison.OrdinalIgnoreCase);
                bool modMatches = !entry.TryGetValue("Mod", out var mod) || string.Equals(mod, modName, StringComparison.OrdinalIgnoreCase);
                if (mapMatches && modMatches)
                    return true;
            }

            return false;
        }
    }
}
