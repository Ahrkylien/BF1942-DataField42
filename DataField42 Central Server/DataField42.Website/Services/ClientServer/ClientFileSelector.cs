using System.Text.Json;
using BF1942.FileSystem;
using DataField42.Website.Configuration;
using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using DataField42.Website.Services.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Services.ClientServer;

/// <summary>
/// Works out which files a client needs to play a map of a mod: the map's own archives, and the
/// files of the mod and of every mod it builds on, as the database knows them.
/// </summary>
public sealed class ClientFileSelector(
    DataField42DbContext context,
    ChecksumRepository checksums,
    IOptions<WebsiteOptions> options,
    ILogger<ClientFileSelector> logger)
{
    /// <summary>
    /// The files every mod has, in the order the game loads them.
    /// </summary>
    private static readonly string[] ModBaseFiles =
    [
        "contentCrc32.con", "init.con", "mod.dll", "lexiconAll.dat", "serverInfo.dds",
        "Archives/ai.rfa", "Archives/aiMeshes.rfa", "Archives/animations.rfa", "Archives/Font.rfa",
        "Archives/menu.rfa", "Archives/menu_001.rfa", "Archives/Objects.rfa", "Archives/shaders.rfa",
        "Archives/sound.rfa", "Archives/sound_001.rfa", "Archives/standardMesh.rfa", "Archives/StandardMesh_001.rfa",
        "Archives/texture.rfa", "Archives/texture_001.rfa", "Archives/treeMesh.rfa", "Archives/bf1942/game.rfa",
    ];

    /// <summary>
    /// The files for <paramref name="mapName"/> of <paramref name="modName"/>, which is none when the
    /// database does not know the mod.
    /// </summary>
    public async Task<List<ClientFile>> SelectAsync(string mapName, string modName, CancellationToken cancellationToken)
    {
        var files = new List<ClientFile>();

        foreach (var mod in await FindModsAsync(modName, cancellationToken))
        {
            string modFolder = Path.Combine(options.Value.ModsDirectory, mod.FolderName!);

            foreach (var (relativePath, fullPath) in await MapArchivesAsync(mod, modFolder, mapName, cancellationToken))
                AddIfOffered(files, await DescribeAsync(mod.FolderName!, relativePath, fullPath, cancellationToken));

            foreach (string relativePath in ModBaseFiles)
            {
                if (SmartPath.Resolve(modFolder, relativePath) is { } fullPath)
                    AddIfOffered(files, await DescribeAsync(mod.FolderName!, relativePath, fullPath, cancellationToken));
            }

            foreach (string folder in new[] { "movies", "music" })
            {
                foreach (string fullPath in VideosIn(modFolder, folder))
                {
                    string relativePath = Path.GetRelativePath(modFolder, fullPath).Replace('\\', '/');
                    AddIfOffered(files, await DescribeAsync(mod.FolderName!, relativePath, fullPath, cancellationToken));
                }
            }
        }

        return files;
    }

    private static void AddIfOffered(List<ClientFile> files, ClientFile? file)
    {
        if (file is not null)
            files.Add(file);
    }

    /// <summary>
    /// The mod itself, then the mods it builds on in the order it lists them. Nothing when the name
    /// is not one mod.
    /// </summary>
    private async Task<List<Mod>> FindModsAsync(string modName, CancellationToken cancellationToken)
    {
        if (await FindOneModAsync(modName, cancellationToken) is not { } main)
            return [];

        var mods = new List<Mod> { main };
        foreach (string dependency in DependencyNames(main))
        {
            if (await FindOneModAsync(dependency, cancellationToken) is { } found)
                mods.Add(found);
        }

        return mods;
    }

    private async Task<Mod?> FindOneModAsync(string folderName, CancellationToken cancellationToken)
    {
        var found = await context.Mods.AsNoTracking().Where(mod => mod.FolderName == folderName).Take(2).ToListAsync(cancellationToken);
        return found.Count == 1 ? found[0] : null;
    }

    /// <summary>
    /// The folder names of the mods a mod lists as <c>game.addModPath</c>, such as <c>Mods/BF1942/</c>
    /// for <c>BF1942</c>, without the mod itself.
    /// </summary>
    private static IEnumerable<string> DependencyNames(Mod mod)
    {
        if (mod.ModDependencies is null)
            return [];

        try
        {
            return
            [
                .. (JsonSerializer.Deserialize<List<string>>(mod.ModDependencies) ?? [])
                    .Select(path => path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
                    .Where(parts => parts.Length > 1 && !parts[1].Equals(mod.FolderName, StringComparison.OrdinalIgnoreCase))
                    .Select(parts => parts[1]),
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The archives of the map in a mod, which the game reads from the mod's levels folder, newest
    /// patch last. A mod that has no such map has none.
    /// </summary>
    private async Task<List<(string RelativePath, string FullPath)>> MapArchivesAsync(Mod mod, string modFolder, string mapName, CancellationToken cancellationToken)
    {
        string name = mapName.Replace('_', ' ');
        var maps = await context.Maps.AsNoTracking().Where(map => map.ModId == mod.Id && map.Name == name).Take(2).ToListAsync(cancellationToken);
        if (maps.Count != 1)
            return [];

        var archives = await context.Rfas.AsNoTracking()
            .Where(rfa => rfa.MapId == maps[0].Id && rfa.Recommended == 1 && rfa.Client == 1)
            .OrderBy(rfa => rfa.PatchNumber)
            .ToListAsync(cancellationToken);

        var result = new List<(string, string)>();
        foreach (var rfa in archives)
        {
            if (rfa.Path is not { Length: > 0 } path || !TransferValidator.IsInside(modFolder, path))
                throw new ClientRequestException("Map file from database is not a path inside the mod");

            string fullPath = Path.Combine(modFolder, path.Replace('\\', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                throw new ClientRequestException("Map file from database does not exist");

            result.Add(("Archives/bf1942/levels/" + rfa.FileName, fullPath));
        }

        return result;
    }

    /// <summary>
    /// The Bink videos in a folder of a mod, whatever the folder's casing.
    /// </summary>
    private static IEnumerable<string> VideosIn(string modFolder, string folder)
    {
        if (SmartPath.Resolve(modFolder, folder, expectDirectory: true) is not { } directory)
            return [];

        return Directory
            .EnumerateFiles(directory, "*.bik", new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive })
            .Order(StringComparer.Ordinal);
    }

    /// <summary>
    /// Describes a file the way a client will read it. A file the client would refuse -- by its name,
    /// or its place in the mod -- would fail the whole download, so it is left out.
    /// </summary>
    private async Task<ClientFile?> DescribeAsync(string mod, string relativePath, string fullPath, CancellationToken cancellationToken)
    {
        relativePath = relativePath.Replace('\\', '/');
        var info = new System.IO.FileInfo(fullPath);
        long modified = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds();

        FileInfo file;
        try
        {
            // Checked before the file is read, which is the slow part.
            file = new FileInfo(mod, relativePath, string.Empty, info.Length, modified);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Not offering {Path} of {Mod}: {Reason}", relativePath, mod, ex.Message);
            return null;
        }

        file.Checksum = await checksums.GetAsync(fullPath, cancellationToken);
        return new ClientFile(file, fullPath);
    }
}
