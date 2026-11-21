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

        return
        [
            new("Double Maps in same Mod", await FindDuplicateMapsAsync(cancellationToken)),
            new("Maps without Mod", await FindMapsWithoutModAsync(cancellationToken)),
            new("Wrong game.mapID (not the same as the mod folderName)", await FindWrongGameMapIdAsync(exceptions, cancellationToken)),
            new("No game.mapID", await FindMissingGameMapIdAsync(exceptions, cancellationToken)),
            new("No gameplaymode at all", await FindMapsWithoutGameplayModeAsync(cancellationToken)),
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
    /// A map whose own <c>init.con</c> names a different mod than the one it is filed under,
    /// which means it was copied between mods without being updated and will not load.
    /// </summary>
    private async Task<List<string>> FindWrongGameMapIdAsync(IntegrityExceptions exceptions, CancellationToken cancellationToken)
    {
        var maps = await context.Maps
            .AsNoTracking()
            .Where(map => map.Mod != null && map.Mod.FolderName != map.GameMapId)
            .OrderBy(map => map.Mod!.FolderName)
            .ThenBy(map => map.GameMapId)
            .Select(map => new { map.Id, map.Name, map.GameMapId, ModFolderName = map.Mod!.FolderName })
            .ToListAsync(cancellationToken);

        return
        [
            .. maps
                .Where(m => !exceptions.IsAllowed("WrongGameMapId", m.Name, m.ModFolderName))
                .Select(m => $"ID: {m.Id}, name: '{m.Name}', gameMapId: '{m.GameMapId}', Mod folderName: {m.ModFolderName}")
        ];
    }

    private async Task<List<string>> FindMissingGameMapIdAsync(IntegrityExceptions exceptions, CancellationToken cancellationToken)
    {
        var maps = await context.Maps
            .AsNoTracking()
            .Where(map => map.GameMapId == null)
            .Select(map => new { map.Id, map.Name, ModFolderName = map.Mod != null ? map.Mod.FolderName : null })
            .ToListAsync(cancellationToken);

        return
        [
            .. maps
                .Where(m => !exceptions.IsAllowed("NoGameMapId", m.Name, m.ModFolderName))
                .Select(m => $"ID: {m.Id}, name: '{m.Name}' Mod folderName: '{m.ModFolderName}'")
        ];
    }

    /// <summary>
    /// A map with no gameplay mode cannot be played at all, so the analysis either has not run
    /// for it or its archives are incomplete.
    /// </summary>
    private async Task<List<string>> FindMapsWithoutGameplayModeAsync(CancellationToken cancellationToken)
    {
        var maps = await context.Maps
            .AsNoTracking()
            .Where(map => map.GpmConquest != 1 && map.GpmCaptureTheFlag != 1 && map.GpmTeamDeathmatch != 1
                && map.GpmCoop != 1 && map.GpmObjectiveMode != 1)
            .Select(map => new { map.Id, map.Name, ModFolderName = map.Mod != null ? map.Mod.FolderName : null })
            .ToListAsync(cancellationToken);

        return [.. maps.Select(m => $"ID: {m.Id}, name: '{m.Name}' Mod folderName: '{m.ModFolderName}'")];
    }

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
