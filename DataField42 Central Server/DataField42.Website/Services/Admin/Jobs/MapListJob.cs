using BF1942.FileSystem;
using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Scans each mod's level archives and brings the <c>Map</c> and <c>RFA</c> tables in line with
/// what is on disk: new archives are added, changed ones updated, and rows whose files are gone
/// removed.
/// </summary>
public sealed class MapListJob(DataField42DbContext context, FileDatabase fileDatabase) : IAdminJob
{
    /// <summary>
    /// Where a mod keeps its level archives, relative to the mod folder.
    /// </summary>
    private const string LevelsPath = "archives/bf1942/levels";

    public string JobId => "MapList";

    public string Name => "Scan maps and archives";

    public string Description =>
        "Walks every mod's level archives and adds, updates or removes the matching map and archive rows. "
        + "Give a mod folder name to scan one mod only.";

    public async Task RunAsync(JobContext jobContext, CancellationToken cancellationToken)
    {
        string? onlyMod = jobContext.GetParameter("mod");

        var mods = await context.Mods
            .Where(mod => mod.FolderName != null && (onlyMod == null || mod.FolderName == onlyMod))
            .OrderBy(mod => mod.FolderName)
            .ToListAsync(cancellationToken);

        if (mods.Count == 0)
            throw new InvalidOperationException(onlyMod is null ? "There are no mods to scan." : $"There is no mod called '{onlyMod}'.");

        jobContext.BeginStep($"Scanning {TextFormatting.Count(mods.Count, "mod")}", mods.Count);

        int addedMaps = 0;
        int addedArchives = 0;
        int removedArchives = 0;

        foreach (var mod in mods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            jobContext.Log($"Scanning {mod.FolderName}");

            var (maps, archives, removed) = await ScanModAsync(mod, jobContext, cancellationToken);
            addedMaps += maps;
            addedArchives += archives;
            removedArchives += removed;

            jobContext.Advance();
        }

        jobContext.Log($"Done: {TextFormatting.Count(addedMaps, "map")} added, {TextFormatting.Count(addedArchives, "archive")} added, {TextFormatting.Count(removedArchives, "archive")} removed.");
    }

    private async Task<(int AddedMaps, int AddedArchives, int RemovedArchives)> ScanModAsync(
        Mod mod,
        JobContext jobContext,
        CancellationToken cancellationToken)
    {
        string modDirectory = fileDatabase.GetModDirectory(mod.FolderName!);
        string? levelsDirectory = SmartPath.Resolve(modDirectory, LevelsPath, expectDirectory: true);
        if (levelsDirectory is null)
        {
            jobContext.Log($"{mod.FolderName}: no levels directory, skipped");
            return (0, 0, 0);
        }

        var onDisk = Directory
            .EnumerateFiles(levelsDirectory, "*.rfa", new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive })
            .Select(path => (Path: path, Name: ParseArchiveName(path)))
            .ToList();

        var existingMaps = await context.Maps
            .Where(map => map.ModId == mod.Id)
            .ToListAsync(cancellationToken);

        var existingArchives = await context.Rfas
            .Where(rfa => rfa.Map != null && rfa.Map.ModId == mod.Id)
            .ToListAsync(cancellationToken);

        var unseenMaps = existingMaps.ToList();
        var unseenArchives = existingArchives.ToList();

        int addedMaps = 0;
        int addedArchives = 0;

        foreach (var group in onDisk.GroupBy(file => file.Name.LevelName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string levelName = group.First().Name.LevelName;

            var map = existingMaps.FirstOrDefault(m =>
                string.Equals(m.NameRaw, levelName, StringComparison.OrdinalIgnoreCase));

            if (map is null)
            {
                map = new Map
                {
                    Name = levelName.Replace('_', ' '),
                    NameRaw = levelName,
                    ModId = mod.Id,
                };
                context.Maps.Add(map);
                existingMaps.Add(map);
                addedMaps++;
                jobContext.Log($"Added map {mod.FolderName}/{levelName}");
            }
            else
            {
                // Rewritten in case only the casing on disk changed.
                map.Name = levelName.Replace('_', ' ');
                map.NameRaw = levelName;
                unseenMaps.Remove(map);
            }

            // The map row needs an id before its archives can reference it.
            await context.SaveChangesAsync(cancellationToken);

            foreach (var (path, name) in group)
            {
                string relativePath = Path.GetRelativePath(modDirectory, path).Replace('\\', '/');
                var modified = File.GetLastWriteTime(path);

                var archive = existingArchives.FirstOrDefault(rfa => rfa.Path == relativePath);
                if (archive is null)
                {
                    context.Rfas.Add(new Rfa
                    {
                        Path = relativePath,
                        PatchNumber = name.PatchNumber,
                        MapId = map.Id,
                        Client = 1,
                        Recommended = 1,
                        Date = modified,
                    });
                    addedArchives++;
                    jobContext.Log($"Added archive {relativePath}");
                }
                else
                {
                    if (archive.PatchNumber != name.PatchNumber || archive.Date != modified || archive.MapId != map.Id)
                    {
                        archive.PatchNumber = name.PatchNumber;
                        archive.Date = modified;
                        archive.MapId = map.Id;
                        jobContext.Log($"Updated archive {relativePath}");
                    }

                    unseenArchives.Remove(archive);
                }
            }
        }

        foreach (var archive in unseenArchives)
        {
            context.Rfas.Remove(archive);
            jobContext.Log($"Removed archive {archive.Path}");
        }

        // A map whose archives have all gone is removed with them.
        foreach (var map in unseenMaps)
        {
            context.Maps.Remove(map);
            jobContext.Log($"Removed map {mod.FolderName}/{map.NameRaw}");
        }

        await context.SaveChangesAsync(cancellationToken);
        return (addedMaps, addedArchives, unseenArchives.Count);
    }

    /// <summary>
    /// Splits an archive's file name into the level it belongs to and its patch number: a
    /// trailing <c>_nnn</c> is a patch of the level named before it, and anything else is the
    /// level's own base archive.
    /// </summary>
    private static (string LevelName, int? PatchNumber) ParseArchiveName(string path)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);

        int lastUnderscore = fileName.LastIndexOf('_');
        if (lastUnderscore < 0)
            return (fileName, null);

        string suffix = fileName[(lastUnderscore + 1)..];
        return suffix.Length > 0 && suffix.All(char.IsAsciiDigit)
            ? (fileName[..lastUnderscore], int.Parse(suffix))
            : (fileName, null);
    }
}
