using System.IO.Compression;
using Force.Crc32;
using Microsoft.Extensions.Options;
using System.Text.Json;
using DataField42.Website.Configuration;
using DataField42.Website.Services.Admin.Jobs;

namespace DataField42.Website.Services.Admin;

/// <summary>
/// Synchronises the collection on this server with the copy kept on a workstation, which is where
/// maps are actually sorted and repackaged.
///
/// The work is split into five stages, of which this server runs two. Stage 1 lists what this
/// server holds. Stages 3 to 5 run on the workstation: it lists its own files, compares the two
/// lists into a set of rules, and packs the files this server is missing. Stage 2, back here,
/// applies those rules and unpacks that archive.
///
/// Doing it this way means the bulk of the collection is never transferred: a map that was only
/// renamed or moved is renamed or moved in place, and only genuinely new files are uploaded.
/// </summary>
public sealed class FileTransferService(IOptions<WebsiteOptions> options, ILogger<FileTransferService> logger)
{
    /// <summary>
    /// The file list this server produces in stage 1 and the workstation compares against.
    /// </summary>
    public const string ServerFileListName = "server.json";

    /// <summary>
    /// The rules the workstation produces in stage 4.
    /// </summary>
    public const string RulesName = "rules.json";

    /// <summary>
    /// The archive of new files the workstation produces in stage 5.
    /// </summary>
    public const string ArchiveName = "newFiles.zip";

    private const int ChecksumBufferSize = 1 << 20;

    private readonly WebsiteOptions _options = options.Value;

    public string WorkingDirectory => _options.FileTransferDirectory;

    public string ServerFileListPath => Path.Combine(WorkingDirectory, ServerFileListName);

    public string RulesPath => Path.Combine(WorkingDirectory, RulesName);

    public string ArchivePath => Path.Combine(WorkingDirectory, ArchiveName);

    public FileInfo? ServerFileListInfo => ToFileInfo(ServerFileListPath);

    public FileInfo? RulesInfo => ToFileInfo(RulesPath);

    public FileInfo? ArchiveInfo => ToFileInfo(ArchivePath);

    /// <summary>
    /// Stage 1: walks the mods directory and writes the file list the workstation compares
    /// against. Checksumming every file is what takes the time, so progress is reported per file.
    /// </summary>
    public async Task BuildServerFileListAsync(JobContext jobContext, CancellationToken cancellationToken)
    {
        string modsDirectory = _options.ModsDirectory;
        if (!Directory.Exists(modsDirectory))
            throw new DirectoryNotFoundException($"The mods directory '{modsDirectory}' does not exist.");

        jobContext.BeginStep("Counting files");
        var files = Directory.EnumerateFiles(modsDirectory, "*", SearchOption.AllDirectories).ToList();

        jobContext.BeginStep($"Checksumming {TextFormatting.Count(files.Count, "file")}", files.Count);

        var root = new FileTreeFolder(null);
        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path.GetRelativePath(modsDirectory, path);
            var folder = root.GetOrAddFolder(Path.GetDirectoryName(relativePath));

            var info = new FileInfo(path);
            folder.Files.Add(new FileTreeFile(
                info.Name,
                ComputeChecksum(path),
                info.Length,
                ToUnixSeconds(info.LastWriteTime)));

            jobContext.Advance();
        }

        jobContext.BeginStep("Writing the file list");
        Directory.CreateDirectory(WorkingDirectory);

        await using var stream = File.Create(ServerFileListPath);
        await JsonSerializer.SerializeAsync(stream, root.ToJson(), cancellationToken: cancellationToken);

        jobContext.Log($"Wrote {ServerFileListName}: {TextFormatting.Count(files.Count, "file")}.");
    }

    /// <summary>
    /// Stores an uploaded rule set, replacing any previous one.
    /// </summary>
    public async Task SaveRulesAsync(Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(WorkingDirectory);
        await using var stream = File.Create(RulesPath);
        await content.CopyToAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Stores an uploaded archive of new files, replacing any previous one.
    /// </summary>
    public async Task SaveArchiveAsync(Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(WorkingDirectory);
        await using var stream = File.Create(ArchivePath);
        await content.CopyToAsync(stream, cancellationToken);
    }

    /// <summary>
    /// Reads the uploaded rules and checks them against what is actually on disk and in the
    /// uploaded archive, so a rule set built against a different state of the collection is
    /// caught before it is applied rather than halfway through.
    /// </summary>
    public async Task<TransferPreview?> PreviewAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(RulesPath))
            return null;

        var rules = await ReadRulesAsync(cancellationToken);
        var archiveEntries = ReadArchiveEntries();

        var missingFromArchive = new List<string>();
        var missingSources = new List<string>();
        var occupiedDestinations = new List<string>();

        string modsDirectory = _options.ModsDirectory;

        foreach (var rule in rules)
        {
            switch (rule.Kind)
            {
                case TransferRuleKind.Add:
                    if (archiveEntries is not null && !archiveEntries.Contains(rule.Path))
                        missingFromArchive.Add(rule.Path);
                    break;

                case TransferRuleKind.Move:
                case TransferRuleKind.Copy:
                    if (!File.Exists(Path.Combine(modsDirectory, rule.Path)))
                        missingSources.Add(rule.Path);
                    if (rule.DestinationPath is { } destination && File.Exists(Path.Combine(modsDirectory, destination)))
                        occupiedDestinations.Add(destination);
                    break;
            }
        }

        var archiveInfo = ArchiveInfo;

        return new TransferPreview
        {
            Rules = rules,
            MissingFromArchive = missingFromArchive,
            MissingSources = missingSources,
            OccupiedDestinations = occupiedDestinations,
            UploadedUtc = RulesInfo?.LastWriteTimeUtc ?? DateTime.UtcNow,
            ArchiveSize = archiveInfo?.Length ?? 0,
            ArchiveEntryCount = archiveEntries?.Count ?? 0,
        };
    }

    /// <summary>
    /// Stage 2: applies the uploaded rules and unpacks the uploaded archive.
    ///
    /// The order matters and is the order the rules were written in: a move frees the place a
    /// later copy needs, and the dates of added files are set only after the archive has been
    /// unpacked, because unpacking overwrites them with the archive's own.
    /// </summary>
    public async Task ApplyAsync(JobContext jobContext, CancellationToken cancellationToken)
    {
        var rules = await ReadRulesAsync(cancellationToken);
        if (rules.Count == 0)
            throw new InvalidOperationException("There are no rules to apply. Upload a rules file first.");

        string modsDirectory = _options.ModsDirectory;

        jobContext.BeginStep("Creating folders");
        foreach (var rule in rules)
        {
            string? target = rule.Kind switch
            {
                TransferRuleKind.Add => rule.Path,
                TransferRuleKind.Move or TransferRuleKind.Copy => rule.DestinationPath,
                _ => null,
            };

            if (target is null)
                continue;

            string? directory = Path.GetDirectoryName(Path.Combine(modsDirectory, target));
            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                jobContext.Log($"Created {directory}");
            }
        }

        jobContext.BeginStep($"Applying {TextFormatting.Count(rules.Count, "rule")}", rules.Count);

        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyRule(rule, modsDirectory, jobContext);
            jobContext.Advance();
        }

        if (File.Exists(ArchivePath))
            ExtractArchive(modsDirectory, jobContext, cancellationToken);
        else
            jobContext.Log($"No {ArchiveName} was uploaded, so no new files were added.");

        jobContext.BeginStep("Setting the dates of added files");
        foreach (var rule in rules.Where(rule => rule.Kind == TransferRuleKind.Add && rule.ModifiedDate.HasValue))
            TrySetLastWriteTime(Path.Combine(modsDirectory, rule.Path), rule.ModifiedDate!.Value, jobContext);

        jobContext.Log("Done.");
    }

    private void ApplyRule(TransferRule rule, string modsDirectory, JobContext jobContext)
    {
        string source = Path.Combine(modsDirectory, rule.Path);
        string? destination = rule.DestinationPath is null ? null : Path.Combine(modsDirectory, rule.DestinationPath);

        switch (rule.Kind)
        {
            case TransferRuleKind.Move when destination is not null:
                if (File.Exists(destination))
                    jobContext.Log($"ERROR: cannot move {rule.Path}, something is already at {rule.DestinationPath}");
                else if (!File.Exists(source))
                    jobContext.Log($"ERROR: cannot move {rule.Path}, it is not there");
                else
                    Move(source, destination, rule, jobContext);
                break;

            case TransferRuleKind.Copy when destination is not null:
                if (File.Exists(destination))
                    jobContext.Log($"ERROR: cannot copy {rule.Path}, something is already at {rule.DestinationPath}");
                else if (!File.Exists(source))
                    jobContext.Log($"ERROR: cannot copy {rule.Path}, it is not there");
                else
                    Copy(source, destination, rule, jobContext);
                break;

            case TransferRuleKind.Delete:
                if (!File.Exists(source))
                    jobContext.Log($"ERROR: cannot delete {rule.Path}, it is not there");
                else
                    Delete(source, rule, jobContext);
                break;

            case TransferRuleKind.ChangeModifiedDate when rule.ModifiedDate.HasValue:
                TrySetLastWriteTime(source, rule.ModifiedDate.Value, jobContext);
                break;

            case TransferRuleKind.Add:
                // Added files come out of the archive, which is unpacked after the rules.
                break;

            case TransferRuleKind.Unknown:
                jobContext.Log($"ERROR: unknown rule '{rule.RawKind}' for {rule.Path}, skipped");
                break;
        }
    }

    private void Move(string source, string destination, TransferRule rule, JobContext jobContext)
    {
        try
        {
            File.Move(source, destination);
            jobContext.Log($"Moved {rule.Path} to {rule.DestinationPath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not move {Source} to {Destination}", source, destination);
            jobContext.Log($"ERROR: cannot move {rule.Path} to {rule.DestinationPath}: {ex.Message}");
        }
    }

    private void Copy(string source, string destination, TransferRule rule, JobContext jobContext)
    {
        try
        {
            File.Copy(source, destination);
            File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
            jobContext.Log($"Copied {rule.Path} to {rule.DestinationPath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not copy {Source} to {Destination}", source, destination);
            jobContext.Log($"ERROR: cannot copy {rule.Path} to {rule.DestinationPath}: {ex.Message}");
        }
    }

    private void Delete(string path, TransferRule rule, JobContext jobContext)
    {
        try
        {
            File.Delete(path);
            jobContext.Log($"Deleted {rule.Path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete {Path}", path);
            jobContext.Log($"ERROR: cannot delete {rule.Path}: {ex.Message}");
        }
    }

    private void TrySetLastWriteTime(string path, DateTime modified, JobContext jobContext)
    {
        try
        {
            if (File.Exists(path))
                File.SetLastWriteTime(path, modified);
            else
                jobContext.Log($"ERROR: cannot set the date of {path}, it is not there");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            logger.LogWarning(ex, "Could not set the modified date of {Path}", path);
        }
    }

    /// <summary>
    /// Unpacks the uploaded archive over the collection. Each entry's date is taken from the
    /// archive, which is the closest thing to the file's real date that a zip carries.
    /// </summary>
    private void ExtractArchive(string modsDirectory, JobContext jobContext, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(ArchivePath);
        jobContext.BeginStep($"Unpacking {TextFormatting.Count(archive.Entries.Count, "file")}", archive.Entries.Count);

        string root = Path.GetFullPath(modsDirectory);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.FullName.EndsWith('/'))
            {
                jobContext.Advance();
                continue;
            }

            string destination = Path.GetFullPath(Path.Combine(root, entry.FullName));

            // An archive entry naming a path outside the collection would write anywhere on the
            // server, so it is refused rather than followed.
            if (!destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                jobContext.Log($"ERROR: refused {entry.FullName}, it points outside the mods directory");
                jobContext.Advance();
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            File.SetLastWriteTime(destination, entry.LastWriteTime.DateTime);

            jobContext.Advance();
        }
    }

    private async Task<List<TransferRule>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(RulesPath))
            return [];

        await using var stream = File.OpenRead(RulesPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"'{RulesName}' does not contain a list of rules.");

        var rules = new List<TransferRule>();
        foreach (var element in document.RootElement.EnumerateArray())
            if (ParseRule(element) is { } rule)
                rules.Add(rule);

        return rules;
    }

    /// <summary>
    /// Parses one rule. Paths are written as lists of path segments, which keeps them free of any
    /// question about which separator the two sides use.
    /// </summary>
    private static TransferRule? ParseRule(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 2)
            return null;

        string rawKind = element[0].GetString() ?? "";
        string? path = ReadPath(element[1]);
        if (path is null)
            return null;

        return rawKind switch
        {
            "delete" => new TransferRule(TransferRuleKind.Delete, path, null, null) { RawKind = rawKind },
            "move" when element.GetArrayLength() >= 3 =>
                new TransferRule(TransferRuleKind.Move, path, ReadPath(element[2]), null) { RawKind = rawKind },
            "copy" when element.GetArrayLength() >= 3 =>
                new TransferRule(TransferRuleKind.Copy, path, ReadPath(element[2]), null) { RawKind = rawKind },
            "change modified date" when element.GetArrayLength() >= 3 =>
                new TransferRule(TransferRuleKind.ChangeModifiedDate, path, null, ReadDate(element[2])) { RawKind = rawKind },
            "add" when element.GetArrayLength() >= 3 =>
                new TransferRule(TransferRuleKind.Add, path, null, ReadDate(element[2])) { RawKind = rawKind },
            _ => new TransferRule(TransferRuleKind.Unknown, path, null, null) { RawKind = rawKind },
        };
    }

    private static string? ReadPath(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return element.ValueKind == JsonValueKind.String ? element.GetString() : null;

        var segments = element.EnumerateArray().Select(segment => segment.GetString()).ToList();
        return segments.Any(segment => segment is null) ? null : string.Join('/', segments);
    }

    private static DateTime? ReadDate(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds((long)element.GetDouble()).LocalDateTime
            : null;

    /// <summary>
    /// The paths inside the uploaded archive, or null when there is no archive to look in.
    /// </summary>
    private HashSet<string>? ReadArchiveEntries()
    {
        if (!File.Exists(ArchivePath))
            return null;

        try
        {
            using var archive = ZipFile.OpenRead(ArchivePath);
            return [.. archive.Entries.Select(entry => entry.FullName.Replace('\\', '/'))];
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            logger.LogWarning(ex, "Could not read the uploaded archive");
            return null;
        }
    }

    /// <summary>
    /// The CRC-32C of a file, as the eight uppercase hex digits the file lists are written with.
    /// Read in chunks rather than whole: the collection holds archives of several hundred
    /// megabytes.
    /// </summary>
    private static string ComputeChecksum(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ChecksumBufferSize, FileOptions.SequentialScan);
        var buffer = new byte[ChecksumBufferSize];

        uint crc = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            crc = Crc32CAlgorithm.Append(crc, buffer, 0, read);

        return $"{crc:X8}";
    }

    private static FileInfo? ToFileInfo(string path) => File.Exists(path) ? new FileInfo(path) : null;

    private static long ToUnixSeconds(DateTime value) => new DateTimeOffset(value).ToUnixTimeSeconds();

    /// <summary>
    /// One file in the list stage 1 writes.
    /// </summary>
    private sealed record FileTreeFile(string Name, string Crc, long Size, long ModifiedUnixSeconds)
    {
        public object?[] ToJson() => [Name, Crc, Size, ModifiedUnixSeconds];
    }

    /// <summary>
    /// One folder in the list stage 1 writes. The format is the one the workstation's own tooling
    /// reads: <c>[name, files, folders]</c>, with a null name at the root.
    /// </summary>
    private sealed class FileTreeFolder(string? name)
    {
        private readonly Dictionary<string, FileTreeFolder> _folders = new(StringComparer.Ordinal);

        public List<FileTreeFile> Files { get; } = [];

        public FileTreeFolder GetOrAddFolder(string? relativeDirectory)
        {
            if (string.IsNullOrEmpty(relativeDirectory) || relativeDirectory == ".")
                return this;

            var current = this;
            foreach (var segment in relativeDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (segment.Length == 0)
                    continue;

                if (!current._folders.TryGetValue(segment, out var child))
                {
                    child = new FileTreeFolder(segment);
                    current._folders[segment] = child;
                }

                current = child;
            }

            return current;
        }

        public object?[] ToJson() =>
        [
            name,
            Files.Select(file => file.ToJson()).ToList(),
            _folders.Values.Select(folder => folder.ToJson()).ToList(),
        ];
    }
}
