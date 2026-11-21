using System.Text;
using BF1942.FileSystem.Rfa;

namespace BF1942.FileSystem;

/// <summary>
/// Exposes one <see cref="RefractorFlatArchive"/> as an <see cref="IBfFileSystem"/>.
/// Builds a case-insensitive name index up front -- the archive's own <c>Entries</c> is a flat list,
/// and this file system gets probed once per script line/include/run, so a linear scan per lookup
/// would be a real cost across a full mod load.
/// </summary>
public sealed class RfaFileSystem : IBfFileSystem
{
    private readonly RefractorFlatArchive _archive;
    private readonly Dictionary<string, RefractorFlatArchiveEntry> _byPath;

    public RfaFileSystem(RefractorFlatArchive archive)
    {
        _archive = archive;
        _byPath = new Dictionary<string, RefractorFlatArchiveEntry>(archive.Entries.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var e in archive.Entries)
            _byPath[BfPath.Normalize(e.Name)] = e;
    }

    public RfaFileSystem(string archivePath) : this(new RefractorFlatArchive(archivePath))
    {
    }

    public bool TryReadBytes(string path, out byte[] data)
    {
        if (_byPath.TryGetValue(BfPath.Normalize(path), out var entry))
        {
            data = _archive.Read(entry);
            return true;
        }
        data = [];
        return false;
    }

    public bool TryReadText(string path, out string text)
    {
        if (TryReadBytes(path, out var data))
        {
            text = Encoding.Latin1.GetString(data);
            return true;
        }
        text = "";
        return false;
    }

    public bool FileExists(string path) => _byPath.ContainsKey(BfPath.Normalize(path));

    public IEnumerable<string> EnumerateFiles(string pathPrefix, string? extension = null)
    {
        string prefix = BfPath.Normalize(pathPrefix);
        foreach (var path in _byPath.Keys)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (extension != null && !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                continue;
            yield return path;
        }
    }
}
