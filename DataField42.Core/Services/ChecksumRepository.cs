using System.Globalization;

/// <summary>
/// The CRC-32C of files, remembered so that a file is only read once for as long as it does not
/// change. Reading a collection of game files takes a long time, and every file listed to or by a
/// client needs its checksum.
/// </summary>
/// <remarks>
/// A file is the same file while its path, size and modification time are. The checksums are kept in
/// a file with one line per file -- checksum, size, date and path, separated by tabs -- added to and
/// not rewritten, so remembering one is cheap however many there are. A file that changed is added
/// again and the later line wins.
/// </remarks>
public sealed class ChecksumRepository(string filePath)
{
    private readonly Lock _lock = new();
    private Dictionary<string, ChecksumRecord>? _records;

    /// <summary>
    /// The checksum of a file, as the eight uppercase hex digits it is listed with.
    /// </summary>
    public string Get(string path)
    {
        var (size, modified) = Describe(path);
        return Find(path, size, modified) ?? Remember(path, size, modified, CheckSums.Crc32C(path));
    }

    /// <inheritdoc cref="Get"/>
    public async Task<string> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        var (size, modified) = Describe(path);
        return Find(path, size, modified) ?? Remember(path, size, modified, await CheckSums.Crc32CAsync(path, cancellationToken));
    }

    private static (long Size, long Modified) Describe(string path)
    {
        var info = new System.IO.FileInfo(path);
        return (info.Length, new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds());
    }

    private string? Find(string path, long size, long modified)
    {
        using (_lock.EnterScope())
        {
            return Records().TryGetValue(path, out var record) && record.Size == size && record.Modified == modified ? record.Checksum : null;
        }
    }

    // The file is read outside the lock: a slow file does not hold up the others.
    private string Remember(string path, long size, long modified, uint crc)
    {
        var record = new ChecksumRecord(path, size, modified, crc.ToString("X8"));

        using (_lock.EnterScope())
        {
            Records()[path] = record;

            // A path with a line break cannot be a line; the file is just read again next time.
            if (!path.Contains('\n'))
                Write(() => File.AppendAllText(filePath, record.ToLine() + "\n"));
        }

        return record.Checksum;
    }

    private Dictionary<string, ChecksumRecord> Records()
    {
        if (_records is not null)
            return _records;

        _records = new Dictionary<string, ChecksumRecord>(StringComparer.Ordinal);
        if (!File.Exists(filePath))
            return _records;

        int lines = 0;
        foreach (string line in File.ReadLines(filePath))
        {
            lines++;

            // A line cut short by a crash is not a record; that file is read again when it is asked for.
            if (ChecksumRecord.TryParse(line) is { } record)
                _records[record.Path] = record;
        }

        // Files that changed leave their old lines behind; once most lines are old, they go.
        if (lines > 2 * _records.Count + 100)
            Write(() => File.WriteAllLines(filePath, _records.Values.Select(record => record.ToLine())));

        return _records;
    }

    // Losing the file only costs reading the files again.
    private void Write(Action write)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? "");
            write();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record ChecksumRecord(string Path, long Size, long Modified, string Checksum)
    {
        public string ToLine() => string.Join('\t', Checksum, Size.ToString(CultureInfo.InvariantCulture), Modified.ToString(CultureInfo.InvariantCulture), Path);

        public static ChecksumRecord? TryParse(string line)
        {
            // The path is last, so a tab in it is part of it.
            var parts = line.Split('\t', 4);
            return parts.Length == 4
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long size)
                && long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long modified)
                    ? new ChecksumRecord(parts[3], size, modified, parts[0])
                    : null;
        }
    }
}
