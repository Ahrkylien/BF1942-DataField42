namespace BF1942.FileSystem;

/// <summary>
/// Case-insensitive path resolution on disk. The game's own scripts and archives are written
/// with inconsistent casing (<c>Archives/bf1942/levels</c> vs. <c>archives/BF1942/Levels</c>),
/// which Windows forgives and Linux does not -- so a path is resolved one segment at a time
/// against what is actually there.
/// </summary>
public static class SmartPath
{
    /// <summary>
    /// Appends <paramref name="relativePath"/> to <paramref name="baseDirectory"/>, matching each
    /// segment case-insensitively. Returns <see langword="null"/> when any segment has no match, or
    /// when the final segment is of the wrong kind (a directory where
    /// <paramref name="expectDirectory"/> is <see langword="false"/>, or vice versa).
    /// </summary>
    public static string? Resolve(string baseDirectory, string relativePath, bool expectDirectory = false)
    {
        if (!Directory.Exists(baseDirectory))
            return null;

        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = baseDirectory;

        for (int i = 0; i < segments.Length; i++)
        {
            bool isLast = i == segments.Length - 1;
            bool wantDirectory = !isLast || expectDirectory;
            string? match = null;

            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                if (!string.Equals(Path.GetFileName(entry), segments[i], StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isDirectory = Directory.Exists(entry);
                if (isDirectory == wantDirectory)
                {
                    match = entry;
                    break;
                }
            }

            if (match is null)
                return null;

            current = match;
        }

        return current;
    }
}
