using DataField42.Website.Data;
using DataField42.Website.Services.ServerQuery;
using Microsoft.EntityFrameworkCore;

namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Recovers the project path each map was built under in BattleCraft, the level editor. The
/// editor writes it into the scripts it generates as a <c>rem (...)</c> comment, which is often
/// the only trace left of who built a map and what else they worked on.
/// </summary>
public sealed class RfaInfoJob(DataField42DbContext context, FileDatabase fileDatabase) : IAdminJob
{
    /// <summary>
    /// The generated scripts that carry the comment, in the order they are worth looking in.
    /// </summary>
    private static readonly string[] GeneratedScripts =
    [
        "ControlPoints.con",
        "ControlPointTemplates.con",
        "ObjectSpawns.con",
        "ObjectSpawnTemplates.con",
        "SoldierSpawns.con",
        "SoldierSpawnTemplates.con",
        "spawnPointManagerSettings.con",
    ];

    /// <summary>
    /// The per-mode folders those scripts live in.
    /// </summary>
    private static readonly string[] GameModeFolders =
    [
        "Conquest",
        "ctf",
        "tdm",
        "ObjectiveMode",
        "SinglePlayer",
    ];

    public string JobId => "RfaInfo";

    public string Name => "Read editor paths";

    public string Description =>
        "Searches every archive for the BattleCraft project path its author built the map under.";

    public async Task RunAsync(JobContext jobContext, CancellationToken cancellationToken)
    {
        var archiveIds = await context.Rfas
            .AsNoTracking()
            .OrderByDescending(rfa => rfa.Id)
            .Select(rfa => rfa.Id)
            .ToListAsync(cancellationToken);

        jobContext.BeginStep($"Searching {TextFormatting.Count(archiveIds.Count, "archive")}", archiveIds.Count);

        int found = 0;

        foreach (var archiveId in archiveIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await ReadArchiveAsync(archiveId, cancellationToken))
                found++;

            jobContext.Advance();

            // Saving in batches keeps a run over tens of thousands of archives from holding every
            // change in memory until the end.
            if (jobContext.CompletedCount % 200 == 0)
                await context.SaveChangesAsync(cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        jobContext.Log($"Done: found a project path in {found} of {TextFormatting.Count(archiveIds.Count, "archive")}.");
    }

    private async Task<bool> ReadArchiveAsync(int archiveId, CancellationToken cancellationToken)
    {
        var archive = await context.Rfas
            .Include(rfa => rfa.Map)
            .ThenInclude(map => map!.Mod)
            .FirstOrDefaultAsync(rfa => rfa.Id == archiveId, cancellationToken);

        if (archive?.Path is null || archive.Map?.Mod?.FolderName is not { } modFolderName || archive.Map.NameRaw is not { } mapNameRaw)
            return false;

        var refractorArchive = fileDatabase.TryOpenArchive(modFolderName, archive.Path);
        if (refractorArchive is null)
            return false;

        string? projectPath = null;

        foreach (var folder in GameModeFolders)
        {
            foreach (var script in GeneratedScripts)
            {
                string entryPath = $"bf1942/levels/{mapNameRaw}/{folder}/{script}";
                var entry = refractorArchive.Entries.FirstOrDefault(e =>
                    string.Equals(e.Name, entryPath, StringComparison.OrdinalIgnoreCase));

                if (entry is null)
                    continue;

                string contents;
                try
                {
                    contents = Encodings.Windows1252.GetString(refractorArchive.Read(entry));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException)
                {
                    continue;
                }

                projectPath = FindProjectPath(contents);
                if (projectPath is not null)
                    break;
            }

            if (projectPath is not null)
                break;
        }

        archive.BattleCraftLevelPath = projectPath;
        return projectPath is not null;
    }

    /// <summary>
    /// The path out of the editor's own comment line, which reads <c>rem (C:\...\LevelName)</c>.
    /// </summary>
    private static string? FindProjectPath(string contents)
    {
        foreach (var line in contents.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("rem (", StringComparison.Ordinal) && trimmed.EndsWith(')'))
                return trimmed[5..^1];
        }

        return null;
    }
}
