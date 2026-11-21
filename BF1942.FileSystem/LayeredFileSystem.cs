namespace BF1942.FileSystem;

/// <summary>
/// An ordered stack of <see cref="IBfFileSystem"/>s, first match wins -- mirrors the real
/// game's own mod-archive-override behavior (a mod's own archives are mounted ahead of the base
/// <c>bf1942</c> ones, so a mod can replace a stock file just by shipping one with the same path).
/// Callers control that priority purely through the order layers are passed in.
/// </summary>
public sealed class LayeredFileSystem(IReadOnlyList<IBfFileSystem> layers) : IBfFileSystem
{
    private readonly IReadOnlyList<IBfFileSystem> _layers = layers;

    public bool TryReadBytes(string path, out byte[] data)
    {
        foreach (var layer in _layers)
            if (layer.TryReadBytes(path, out data))
                return true;
        data = [];
        return false;
    }

    public bool TryReadText(string path, out string text)
    {
        foreach (var layer in _layers)
            if (layer.TryReadText(path, out text))
                return true;
        text = "";
        return false;
    }

    public bool FileExists(string path)
    {
        foreach (var layer in _layers)
            if (layer.FileExists(path))
                return true;
        return false;
    }

    public IEnumerable<string> EnumerateFiles(string pathPrefix, string? extension = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in _layers)
            foreach (var path in layer.EnumerateFiles(pathPrefix, extension))
                if (seen.Add(path))
                    yield return path;
    }
}
