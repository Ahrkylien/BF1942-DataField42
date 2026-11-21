using System.IO.Compression;
using System.Text;
using BF1942.FileSystem.Rfa;
using DataField42.Website.Configuration;
using DataField42.Website.Data.Entities;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Services;

/// <summary>
/// The mod files on disk: the archives maps are packed in, and the readmes and screenshots their
/// authors shipped alongside them.
/// </summary>
public sealed class FileDatabase(IOptions<WebsiteOptions> options, IWebHostEnvironment environment, ILogger<FileDatabase> logger)
{
    private static readonly string[] ReadmeImageExtensions = [".png", ".jpg", ".tga", ".dds", ".gif"];

    private readonly WebsiteOptions _options = options.Value;

    public string ModsDirectory => _options.ModsDirectory;

    /// <summary>
    /// Where the minimaps the map analysis extracts are written, inside the web root so they are
    /// served as ordinary static files.
    /// </summary>
    public string InGameMapsDirectory => Path.Combine(environment.WebRootPath, "images", "ingamemaps");

    public string GetModDirectory(string modFolderName) => Path.Combine(ModsDirectory, modFolderName);

    /// <summary>
    /// The absolute path of an archive whose <see cref="Rfa.Path"/> is relative to its mod.
    /// </summary>
    public string GetRfaPath(string modFolderName, string rfaRelativePath) =>
        Path.Combine(GetModDirectory(modFolderName), rfaRelativePath.Replace('\\', Path.DirectorySeparatorChar));

    /// <summary>
    /// Opens an archive, or returns <see langword="null"/> when it is missing or unreadable --
    /// the database can outlive the file it points at.
    /// </summary>
    public RefractorFlatArchive? TryOpenArchive(string modFolderName, string rfaRelativePath)
    {
        string path = GetRfaPath(modFolderName, rfaRelativePath);
        try
        {
            return new RefractorFlatArchive(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not open archive {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// The contents of a map's readme, with email addresses removed unless
    /// <paramref name="showEmailAddresses"/> says otherwise. Readmes are from the early 2000s and
    /// are not valid UTF-8 nearly often enough to decode strictly.
    /// </summary>
    public string? TryReadReadme(Map map, string modFolderName, bool showEmailAddresses)
    {
        if (map.Readme is null || map.Folder is null)
            return null;

        string path = Path.Combine(GetModDirectory(modFolderName), map.Folder, map.Readme);
        if (!File.Exists(path))
            return null;

        try
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            return showEmailAddresses ? text : TextFormatting.RemoveEmailAddresses(text);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not read readme {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// The URL of a map's minimap, falling back to the placeholder when the analysis found none.
    /// </summary>
    public string GetInGameMapUrl(int mapId)
    {
        foreach (var extension in new[] { ".jpg", ".png" })
            if (File.Exists(Path.Combine(InGameMapsDirectory, mapId + extension)))
                return $"/static/images/ingamemaps/{mapId}{extension}";

        return "/static/images/ingamemap_default.png";
    }

    /// <summary>
    /// The URL that serves one of a map's screenshots. The path is hex-encoded because these are
    /// author-chosen file names in author-chosen folders, and hex keeps every byte of them intact
    /// through a URL without any escaping question.
    /// </summary>
    public string GetMapImageUrl(string modFolderName, string mapFolder, string imageName)
    {
        string relativePath = $"{modFolderName}/{mapFolder}/{imageName}".Replace('\\', '/');
        return "/MapImg/" + Convert.ToHexString(Encoding.UTF8.GetBytes(relativePath)).ToLowerInvariant();
    }

    /// <summary>
    /// Resolves a hex-encoded image path back to a file inside the mods directory. Returns
    /// <see langword="null"/> when it does not decode, does not exist, or points outside the mods
    /// directory.
    /// </summary>
    public string? ResolveMapImagePath(string hexPath)
    {
        string relativePath;
        try
        {
            relativePath = Encoding.UTF8.GetString(Convert.FromHexString(hexPath));
        }
        catch (FormatException)
        {
            return null;
        }

        string modsRoot = Path.GetFullPath(ModsDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(modsRoot, relativePath.Replace('\\', '/')));

        if (!fullPath.StartsWith(modsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return null;

        return File.Exists(fullPath) ? fullPath : null;
    }

    /// <summary>
    /// Packs a map's archives into a zip for download, flattened to their file names -- which is
    /// how they have to be laid out in a mod's levels folder anyway.
    /// </summary>
    public async Task<byte[]> CreateMapArchiveAsync(string modFolderName, IEnumerable<Rfa> rfas, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var rfa in rfas)
            {
                if (rfa.Path is null)
                    continue;

                string path = GetRfaPath(modFolderName, rfa.Path);
                if (!File.Exists(path))
                    continue;

                var entry = archive.CreateEntry(Path.GetFileName(path), CompressionLevel.Optimal);
                entry.LastWriteTime = File.GetLastWriteTime(path);

                await using var entryStream = entry.Open();
                await using var fileStream = File.OpenRead(path);
                await fileStream.CopyToAsync(entryStream, cancellationToken);
            }

        return buffer.ToArray();
    }

    /// <summary>
    /// The readme and screenshots sitting beside a map's archives, as paths relative to
    /// <paramref name="mapFolder"/>. A readme named <c>readme.txt</c> wins; failing that, the
    /// first text file found.
    /// </summary>
    public (string? Readme, List<string> Images) FindReadmeAndImages(string mapFolder)
    {
        var images = new List<string>();
        string? readme = null;
        string? firstTextFile = null;

        if (!Directory.Exists(mapFolder))
            return (null, images);

        foreach (var file in Directory.EnumerateFiles(mapFolder, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(mapFolder, file);

            if (readme is null && file.EndsWith("readme.txt", StringComparison.OrdinalIgnoreCase))
                readme = relativePath;
            else if (firstTextFile is null && file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                firstTextFile = relativePath;

            if (ReadmeImageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                images.Add(relativePath);
        }

        return (readme ?? firstTextFile, images);
    }
}
