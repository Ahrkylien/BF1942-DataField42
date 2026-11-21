namespace BF1942.FileSystem;

/// <summary>
/// Exposes a plain directory tree as an <see cref="IBfFileSystem"/> -- for unpacked/dev-mode
/// mods, where the real game's <c>Objects/</c> tree hasn't been packed into <c>.rfa</c> archives yet.
/// A real install only ever has this on disk (see <c>init.con</c>'s <c>Objects_001</c> overlay
/// comments) never in a released archive-only distribution, but it keeps the loader usable while
/// developing/testing against extracted content.
/// </summary>
public sealed class DirectoryFileSystem(string rootDirectory) : IBfFileSystem
{
    private readonly string _root = Path.GetFullPath(rootDirectory);

    private string ToFullPath(string path) => Path.Combine(_root, BfPath.Normalize(path).Replace('/', Path.DirectorySeparatorChar));

    public bool TryReadBytes(string path, out byte[] data)
    {
        string full = ToFullPath(path);
        if (!File.Exists(full))
        { data = []; return false; }
        data = File.ReadAllBytes(full);
        return true;
    }

    public bool TryReadText(string path, out string text)
    {
        string full = ToFullPath(path);
        if (!File.Exists(full))
        { text = ""; return false; }
        text = File.ReadAllText(full);
        return true;
    }

    public bool FileExists(string path) => File.Exists(ToFullPath(path));

    public IEnumerable<string> EnumerateFiles(string pathPrefix, string? extension = null)
    {
        string full = ToFullPath(pathPrefix);
        string dir = Directory.Exists(full) ? full : Path.GetDirectoryName(full) ?? _root;
        if (!Directory.Exists(dir))
            yield break;

        string pattern = extension is null ? "*" : "*" + extension;
        foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
        {
            string rel = BfPath.Normalize(Path.GetRelativePath(_root, file));
            if (rel.StartsWith(BfPath.Normalize(pathPrefix), StringComparison.OrdinalIgnoreCase))
                yield return rel;
        }
    }
}
