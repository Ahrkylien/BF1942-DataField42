namespace BF1942.FileSystem;

/// <summary>
/// A path-addressable source of the game's own script/asset files. Paths are forward-slash,
/// case-insensitive (matches the game's own convention -- see e.g. "Objects/Soldiers/BritishSoldier/
/// Objects.con"). Implementations back onto a single <see cref="Rfa.RefractorFlatArchive"/>, a plain
/// directory, or a priority-ordered stack of both (<see cref="LayeredFileSystem"/>) -- mirroring the
/// real engine's own split between FlatArchiveHandler/Win32FileSystem/UnixFileSystem and FileManager.
/// </summary>
public interface IBfFileSystem
{
    bool TryReadBytes(string path, out byte[] data);

    bool TryReadText(string path, out string text);

    bool FileExists(string path);

    /// <summary>
    /// Every file whose path starts with <paramref name="pathPrefix"/>, optionally filtered
    /// to a single extension (e.g. ".con"). Order is implementation-defined but stable.
    /// </summary>
    IEnumerable<string> EnumerateFiles(string pathPrefix, string? extension = null);
}
