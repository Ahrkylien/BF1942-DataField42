namespace BF1942.FileSystem;

public static class BfPath
{
    /// <summary>
    /// Backslash -> forward-slash, no leading slash -- the normal form every
    /// <see cref="IBfFileSystem"/> implementation stores/compares paths in.
    /// </summary>
    public static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
