using System.Text.Json;
using DataField42.Website.Services.MapAnalysis;
using BF1942.Core.Scripting;
using BF1942.FileSystem;
using DataField42.Website.Services.Imaging;
using BF1942.Lexicon;
using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Reads each map's archives with the game's own script engine and records what it finds: which
/// gameplay modes the map has, where its control points and vehicle spawns are, which mod its
/// scripts claim it belongs to, its briefing text, the readme and screenshots shipped with it,
/// and its minimap.
/// </summary>
public sealed class MapInfoJob(
    DataField42DbContext context,
    FileDatabase fileDatabase,
    ILogger<MapInfoJob> logger) : IAdminJob
{
    /// <summary>
    /// Where a mod keeps its level archives, relative to the mod folder.
    /// </summary>
    private const string LevelsPath = "archives/bf1942/levels";

    /// <summary>
    /// How many of a map's script errors are listed; an old map can have hundreds.
    /// </summary>
    private const int MaxScriptErrorsLogged = 5;

    /// <summary>
    /// The kinds of script error that are not worth a line in the output: most maps run scripts that
    /// are not there, and nobody can do anything about it.
    /// </summary>
    private static readonly ScriptErrorKind[] QuietScriptErrors = [ScriptErrorKind.FileNotFound];

    private readonly HashSet<(string Path, string Problem)> _reportedArchiveProblems = [];

    private readonly Dictionary<string, LexiconFile> _lexiconsByMod = new(StringComparer.OrdinalIgnoreCase);

    public string JobId => "MapInfo";

    public string Name => "Analyse maps";

    public string Description =>
        "Opens every map's archives, runs its scripts, and records gameplay modes, control points, "
        + "object spawns, briefing text, readme, screenshots and minimap. "
        + "Give a mod folder name to analyse one mod only.";

    public async Task RunAsync(JobContext jobContext, CancellationToken cancellationToken)
    {
        string? onlyMod = jobContext.GetParameter("mod");

        var mods = await context.Mods
            .AsNoTracking()
            .Where(mod => mod.FolderName != null && (onlyMod == null || mod.FolderName == onlyMod))
            .OrderByDescending(mod => mod.Id)
            .ToListAsync(cancellationToken);

        if (mods.Count == 0)
            throw new InvalidOperationException(onlyMod is null ? "There are no mods to analyse." : $"There is no mod called '{onlyMod}'.");

        int totalMaps = await context.Maps
            .CountAsync(map => map.ModId != null && mods.Select(mod => mod.Id).Contains(map.ModId.Value), cancellationToken);

        jobContext.BeginStep($"Analysing {TextFormatting.Count(totalMaps, "map")} in {TextFormatting.Count(mods.Count, "mod")}", totalMaps);

        int analysed = 0;
        int skipped = 0;

        foreach (var mod in mods)
        {
            var mapIds = await context.Maps
                .AsNoTracking()
                .Where(map => map.ModId == mod.Id)
                .OrderBy(map => map.Id)
                .Select(map => map.Id)
                .ToListAsync(cancellationToken);

            if (mapIds.Count > 0)
                jobContext.Log($"{mod.FolderName}: {TextFormatting.Count(mapIds.Count, "map")}");

            foreach (var mapId in mapIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await AnalyseMapAsync(mapId, jobContext, cancellationToken))
                    analysed++;
                else
                    skipped++;

                jobContext.Advance();
            }
        }

        jobContext.Log($"Done: {analysed} analysed, {skipped} skipped.");
    }

    /// <summary>
    /// Analyses one map. Returns false when the map has no archives to read, which happens while
    /// a map is being added.
    /// </summary>
    private async Task<bool> AnalyseMapAsync(int mapId, JobContext jobContext, CancellationToken cancellationToken)
    {
        var map = await context.Maps
            .Include(m => m.Mod)
            .FirstOrDefaultAsync(m => m.Id == mapId, cancellationToken);

        if (map?.Mod?.FolderName is not { } modFolderName || map.NameRaw is not { } mapNameRaw)
            return false;

        string modDirectory = fileDatabase.GetModDirectory(modFolderName);
        var dependencyMods = await ResolveDependencyModsAsync(map.Mod, cancellationToken);

        var ownArchives = await context.Rfas
            .Where(rfa => rfa.MapId == mapId && rfa.Client == 1 && rfa.Recommended == 1)
            .ToListAsync(cancellationToken);

        if (ownArchives.Count == 0)
            return false;

        var archivePaths = await BuildArchiveStackAsync(map, modFolderName, ownArchives, dependencyMods, cancellationToken);
        var lexicon = ReadLexicon(map.Mod, dependencyMods);

        var analysis = LevelAnalyzer.Analyze(archivePaths, modDirectory, mapNameRaw);

        // An archive that several maps are read from is reported once.
        foreach (var (archivePath, problem) in analysis.ArchiveProblems.Where(item => _reportedArchiveProblems.Add(item)))
            jobContext.Log($"WARNING: {archivePath} {problem}");

        var scriptErrors = analysis.Errors.Where(error => !QuietScriptErrors.Contains(error.Kind)).ToList();

        foreach (var error in scriptErrors.Take(MaxScriptErrorsLogged))
            jobContext.Log($"WARNING: {mapNameRaw}: {error.Path}:{error.LineNumber}: {error.Message}");

        if (scriptErrors.Count > MaxScriptErrorsLogged)
            jobContext.Log($"WARNING: {mapNameRaw}: and {scriptErrors.Count - MaxScriptErrorsLogged} more script errors");

        foreach (var archivePath in analysis.MismatchedArchiveNames)
            jobContext.Log($"WARNING: {archivePath} does not contain a level folder called {mapNameRaw}");

        ApplyLevelData(map, analysis, lexicon);
        ApplyArchiveMetadata(map, modDirectory, ownArchives);

        await context.SaveChangesAsync(cancellationToken);

        ExtractMinimap(map, mapNameRaw, analysis, jobContext);
        return true;
    }

    /// <summary>
    /// The archives to read, highest priority first: the map's own newest patch down to its base
    /// archive, then the same map from each mod this mod depends on, and finally the mod's own
    /// object archive, which is where the templates the level places are defined.
    /// </summary>
    private async Task<List<string>> BuildArchiveStackAsync(
        Map map,
        string modFolderName,
        List<Rfa> ownArchives,
        List<Mod> dependencyMods,
        CancellationToken cancellationToken)
    {
        var paths = ownArchives
            .OrderByDescending(rfa => rfa.PatchNumber ?? -1)
            .Where(rfa => rfa.Path is not null)
            .Select(rfa => fileDatabase.GetRfaPath(modFolderName, rfa.Path!))
            .ToList();

        foreach (var dependencyMod in dependencyMods)
        {
            if (dependencyMod.FolderName is not { } dependencyFolderName)
                continue;

            var dependencyMap = await context.Maps
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ModId == dependencyMod.Id && m.Name == map.Name, cancellationToken);

            if (dependencyMap is null)
                continue;

            var dependencyArchives = await context.Rfas
                .AsNoTracking()
                .Where(rfa => rfa.MapId == dependencyMap.Id && rfa.Client == 1 && rfa.Recommended == 1)
                .ToListAsync(cancellationToken);

            paths.AddRange(dependencyArchives
                .OrderByDescending(rfa => rfa.PatchNumber ?? -1)
                .Where(rfa => rfa.Path is not null)
                .Select(rfa => fileDatabase.GetRfaPath(dependencyFolderName, rfa.Path!)));
        }

        if (SmartPath.Resolve(fileDatabase.GetModDirectory(modFolderName), "Archives/Objects.rfa") is { } objectsArchive)
            paths.Add(objectsArchive);

        return paths;
    }

    /// <summary>
    /// The mods a mod depends on, in declaration order, as rows.
    /// </summary>
    private async Task<List<Mod>> ResolveDependencyModsAsync(Mod mod, CancellationToken cancellationToken)
    {
        var mods = new List<Mod>();

        foreach (var folderName in ParseDependencyFolderNames(mod))
        {
            var dependency = await context.Mods
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.FolderName == folderName, cancellationToken);

            if (dependency is not null)
                mods.Add(dependency);
        }

        return mods;
    }

    private static List<string> ParseDependencyFolderNames(Mod mod)
    {
        if (mod.ModDependencies is null || mod.FolderName is null)
            return [];

        try
        {
            var paths = JsonSerializer.Deserialize<List<string>>(mod.ModDependencies) ?? [];
            return
            [
                .. paths
                    .Select(path => path.Replace('\\', '/').TrimEnd('/').Split('/').Last())
                    .Where(name => name.Length > 0 && !name.Equals(mod.FolderName, StringComparison.OrdinalIgnoreCase))
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The display-name table to resolve control point and vehicle names through: the mods this
    /// mod depends on first, so the mod's own strings override theirs. Lexicons are cached across
    /// the run, because every map of a mod resolves against the same ones.
    /// </summary>
    private LexiconFile ReadLexicon(Mod mod, List<Mod> dependencyMods)
    {
        var lexicon = LexiconFile.Empty;

        foreach (var current in Enumerable.Reverse(dependencyMods).Append(mod))
        {
            if (current.FolderName is not { } folderName)
                continue;

            if (!_lexiconsByMod.TryGetValue(folderName, out var modLexicon))
            {
                string? path = SmartPath.Resolve(fileDatabase.GetModDirectory(folderName), "lexiconAll.dat");
                modLexicon = (path is null ? null : LexiconFile.TryRead(path)) ?? LexiconFile.Empty;
                _lexiconsByMod[folderName] = modLexicon;
            }

            lexicon = lexicon.Overlay(modLexicon);
        }

        return lexicon;
    }

    /// <summary>
    /// Records what the level's scripts declared.
    /// </summary>
    private static void ApplyLevelData(Map map, LevelAnalysis analysis, LexiconFile lexicon)
    {
        var modes = analysis.GameplayModes;
        map.GpmConquest = modes.Conquest ? 1 : 0;
        map.GpmCaptureTheFlag = modes.CaptureTheFlag ? 1 : 0;
        map.GpmTeamDeathmatch = modes.TeamDeathmatch ? 1 : 0;
        map.GpmCoop = modes.Coop ? 1 : 0;
        map.GpmObjectiveMode = modes.ObjectiveMode ? 1 : 0;

        var data = analysis.Data;
        map.GameMapId = data.Game.MapId;
        map.MultiplayerBriefingObjectives =
            lexicon.Resolve(data.Game.MultiplayerBriefingObjectives)
            ?? lexicon.Resolve(data.Game.ObjectiveBriefing);

        var controlPoints = new List<object?[]>();
        var objectSpawns = new List<object?[]>();

        foreach (var levelObject in data.Objects)
        {
            if (levelObject.Template is not { } template)
                continue;

            if (template.IsControlPoint)
            {
                controlPoints.Add(
                [
                    lexicon.Resolve(template.ControlPointName),
                    template.Team,
                    template.UnableToChangeTeam,
                    levelObject.AbsolutePosition.ToArray(),
                ]);
            }
            else if (template.IsObjectSpawner)
            {
                objectSpawns.Add(
                [
                    levelObject.AbsolutePosition.ToArray(),
                    levelObject.OSId,
                    levelObject.Team,
                    template.Name,
                    template.SpawnedTemplateNames.ToDictionary(
                        spawned => spawned.Key.ToString(),
                        spawned => lexicon.Resolve(spawned.Value)),
                ]);
            }
        }

        // Without a combat area the markers have no frame to be positioned in, so the console's
        // world size stands in for it: a square world starting at the origin.
        var combatArea = data.Game.ActiveCombatArea
            ?? (data.ConsoleWorldSize is { } worldSize ? [0, 0, worldSize, worldSize] : null);

        map.ControlPointsInfo = combatArea is null
            ? null
            : JsonSerializer.Serialize(new object[] { combatArea, controlPoints });
        map.ObjectSpawnsInfo = objectSpawns.Count == 0 ? null : JsonSerializer.Serialize(objectSpawns);
    }

    /// <summary>
    /// Records what sits beside the map's archives on disk: the folder it was shipped in, the
    /// readme and screenshots in it, and the dates of its archives. A map whose archives sit
    /// directly in the mod's levels folder was not shipped in a folder of its own, so it has
    /// none of these.
    /// </summary>
    private void ApplyArchiveMetadata(Map map, string modDirectory, List<Rfa> ownArchives)
    {
        var sorted = ownArchives.OrderBy(rfa => rfa.PatchNumber ?? -1).ToList();
        string? mainArchivePath = sorted.FirstOrDefault(rfa => rfa.IsRecommended)?.Path ?? sorted.FirstOrDefault()?.Path;

        string relativeDirectory = Path.GetDirectoryName(mainArchivePath?.Replace('\\', '/') ?? "") ?? "";
        string archiveDirectory = Path.GetFullPath(Path.Combine(modDirectory, relativeDirectory));
        string? levelsDirectory = SmartPath.Resolve(modDirectory, LevelsPath, expectDirectory: true);

        if (levelsDirectory is not null && string.Equals(
                Path.GetFullPath(levelsDirectory).TrimEnd(Path.DirectorySeparatorChar),
                archiveDirectory.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            map.Folder = null;
            map.Readme = null;
            map.Images = null;
        }
        else
        {
            var (readme, images) = fileDatabase.FindReadmeAndImages(archiveDirectory);
            map.Folder = relativeDirectory.Length == 0 ? null : relativeDirectory;
            map.Readme = readme;
            map.Images = images.Count == 0 ? null : JsonSerializer.Serialize(images);
        }

        map.DateOldestRfa = ownArchives.Min(rfa => rfa.Date);
        map.DateNewestRfa = ownArchives.Max(rfa => rfa.Date);
    }

    /// <summary>
    /// Extracts the map's minimap and converts it to something a browser can show. The old one is
    /// removed first, so a map whose minimap has gone does not keep showing the previous one.
    /// </summary>
    private void ExtractMinimap(Map map, string mapNameRaw, LevelAnalysis analysis, JobContext jobContext)
    {
        foreach (var extension in new[] { ".jpg", ".png" })
        {
            string existing = Path.Combine(fileDatabase.InGameMapsDirectory, map.Id + extension);
            if (File.Exists(existing))
                File.Delete(existing);
        }

        string basePath = $"bf1942/levels/{mapNameRaw}/textures/ingamemap";
        string? sourcePath = new[] { $"{basePath}.dds", $"{basePath}.tga" }
            .FirstOrDefault(analysis.FileSystem.FileExists);

        if (sourcePath is null || !analysis.FileSystem.TryReadBytes(sourcePath, out var data))
            return;

        string temporaryPath = Path.Combine(Path.GetTempPath(), $"df42-minimap-{map.Id}{Path.GetExtension(sourcePath)}");
        try
        {
            Directory.CreateDirectory(fileDatabase.InGameMapsDirectory);
            File.WriteAllBytes(temporaryPath, data);

            if (GameImageConverter.Convert(temporaryPath, Path.Combine(fileDatabase.InGameMapsDirectory, map.Id.ToString())) is null)
                jobContext.Log($"WARNING: could not convert the minimap of {mapNameRaw}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write the minimap of map {MapId}", map.Id);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
