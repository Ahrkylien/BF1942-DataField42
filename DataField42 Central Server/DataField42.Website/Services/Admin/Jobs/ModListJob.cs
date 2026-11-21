using System.Text.Json;
using DataField42.Website.Services.MapAnalysis;
using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using DataField42.Website.Services;
using Microsoft.EntityFrameworkCore;
using YamlDotNet.Serialization;

namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Scans the mods directory and brings the <c>Mod</c> table in line with it, reading each mod's
/// own <c>init.con</c> for its name, version and the mods it depends on.
/// </summary>
public sealed class ModListJob(
    DataField42DbContext context,
    FileDatabase fileDatabase,
    IWebHostEnvironment environment,
    ILogger<ModListJob> logger) : IAdminJob
{
    public string JobId => "ModList";

    public string Name => "Scan mods";

    public string Description =>
        "Reads every mod folder's init.con and adds or updates its name, version and dependencies.";

    public async Task RunAsync(JobContext jobContext, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(fileDatabase.ModsDirectory))
            throw new DirectoryNotFoundException($"The mods directory '{fileDatabase.ModsDirectory}' does not exist.");

        var fullNameOverrides = ReadFullNameOverrides();
        var modDirectories = Directory.GetDirectories(fileDatabase.ModsDirectory);

        jobContext.BeginStep("Reading mod folders", modDirectories.Length);

        int added = 0;
        int updated = 0;

        foreach (var modDirectory in modDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string folderName = Path.GetFileName(modDirectory);
            var gameInfo = LevelAnalyzer.ReadModInfo(modDirectory);

            string? fullName = fullNameOverrides.GetValueOrDefault(folderName) ?? gameInfo.CustomGameName;
            string dependencies = JsonSerializer.Serialize(gameInfo.ModPaths);

            var mod = await context.Mods.FirstOrDefaultAsync(m => m.FolderName == folderName, cancellationToken);
            if (mod is null)
            {
                context.Mods.Add(new Mod
                {
                    FolderName = folderName,
                    FullName = fullName,
                    Version = gameInfo.CustomGameVersion,
                    ModDependencies = dependencies,
                });
                added++;
                jobContext.Log($"Added mod {folderName}");
            }
            else if (mod.FullName != fullName || mod.Version != gameInfo.CustomGameVersion || mod.ModDependencies != dependencies)
            {
                mod.FullName = fullName;
                mod.Version = gameInfo.CustomGameVersion;
                mod.ModDependencies = dependencies;
                updated++;
                jobContext.Log($"Updated mod {folderName}");
            }

            jobContext.Advance();
        }

        await context.SaveChangesAsync(cancellationToken);
        jobContext.Log($"Done: {added} added, {updated} updated, {TextFormatting.Count(modDirectories.Length, "folder")} scanned.");
    }

    /// <summary>
    /// Display names for mods whose own <c>init.con</c> gives a name that is missing, wrong or
    /// unhelpful, read from <c>Resources/modInfo.yaml</c>.
    /// </summary>
    private Dictionary<string, string> ReadFullNameOverrides()
    {
        string path = Path.Combine(environment.ContentRootPath, "Resources", "modInfo.yaml");
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = new DeserializerBuilder().Build()
                .Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path)) ?? [];

            var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (folderName, properties) in parsed)
                if (properties.TryGetValue("fullName", out var fullName))
                    overrides[folderName] = fullName;

            return overrides;
        }
        catch (Exception ex) when (ex is IOException or YamlDotNet.Core.YamlException)
        {
            logger.LogWarning(ex, "Could not read mod name overrides from {Path}", path);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
