using BF1942.Core.Scripting;
using BF1942.FileSystem.Rfa;

using BF1942.FileSystem;

namespace DataField42.Website.Services.MapAnalysis;

/// <summary>
/// Reads a level the way the game does: a stack of archives with the map's own patches on top,
/// then the mod's and the level's scripts in load order.
/// </summary>
public static class LevelAnalyzer
{
    private static readonly string[] GameModeScripts = ["conquest", "coop", "tdm", "objectivemode", "ctf"];

    /// <summary>
    /// Analyzes the level named <paramref name="mapNameRaw"/> (the on-disk name, underscores and
    /// all) from <paramref name="archivePaths"/>, which must be ordered highest-priority first:
    /// the map's newest patch archive, its older patches, the same map from any mod this mod
    /// depends on, and finally the mod's <c>Objects.rfa</c>.
    /// </summary>
    public static LevelAnalysis Analyze(IReadOnlyList<string> archivePaths, string modDirectory, string mapNameRaw)
    {
        var layers = new List<IBfFileSystem>();
        var mismatchedArchiveNames = new List<string>();
        var unreadableArchives = new List<(string Path, string Reason)>();

        foreach (var archivePath in archivePaths)
        {
            RefractorFlatArchive archive;
            try
            {
                archive = new RefractorFlatArchive(archivePath);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException)
            {
                unreadableArchives.Add((archivePath, ex.Message));
                continue;
            }

            if (HasMismatchedInternalName(archive, mapNameRaw))
                mismatchedArchiveNames.Add(archivePath);

            layers.Add(new RfaFileSystem(archive));
        }

        var fileSystem = new LayeredFileSystem(layers);
        var data = new LevelData();
        var constants = new ScriptConstants();
        var handler = new LevelScriptHandler(data);
        var errors = new List<ScriptError>();

        string levelPath = $"bf1942/levels/{mapNameRaw}";
        var modes = new GameplayModes(
            Conquest: fileSystem.FileExists($"{levelPath}/gametypes/conquest.con"),
            CaptureTheFlag: fileSystem.FileExists($"{levelPath}/gametypes/ctf.con"),
            TeamDeathmatch: fileSystem.FileExists($"{levelPath}/gametypes/tdm.con"),
            Coop: fileSystem.FileExists($"{levelPath}/gametypes/coop.con"),
            ObjectiveMode: fileSystem.FileExists($"{levelPath}/gametypes/objectivemode.con"));

        // The mod's init.con lives on disk beside its archives, not inside one.
        string? modInitCon = SmartPath.Resolve(modDirectory, "init.con");
        if (modInitCon is not null)
        {
            var diskReader = new ScriptReader(new DirectoryFileSystem(modDirectory), constants, handler);
            diskReader.Read(Path.GetFileName(modInitCon));
            errors.AddRange(diskReader.Errors);
        }

        var reader = new ScriptReader(fileSystem, constants, handler);
        reader.Read($"{levelPath}/menu/init.con");
        reader.Read($"{levelPath}/init.con", ["host"]);

        // Only the first mode the level both declares and ships a root script for is read: the
        // engine loads exactly one, and reading more would merge objects from several modes.
        foreach (var mode in GameModeScripts)
        {
            if (!fileSystem.FileExists($"{levelPath}/gametypes/{mode}.con"))
                continue;
            if (!fileSystem.FileExists($"{levelPath}/{mode}.con"))
                continue;

            reader.Read($"{levelPath}/{mode}.con", ["host"]);
            break;
        }

        errors.AddRange(reader.Errors);
        data.Link();

        return new LevelAnalysis
        {
            Data = data,
            FileSystem = fileSystem,
            GameplayModes = modes,
            Errors = errors,
            MismatchedArchiveNames = mismatchedArchiveNames,
            UnreadableArchives = unreadableArchives,
        };
    }

    /// <summary>
    /// Reads a mod's <c>init.con</c> on its own, for the mod's name, version and the mods it
    /// depends on.
    /// </summary>
    public static GameInfo ReadModInfo(string modDirectory)
    {
        var data = new LevelData();
        string? initCon = SmartPath.Resolve(modDirectory, "init.con");
        if (initCon is null)
            return data.Game;

        var reader = new ScriptReader(new DirectoryFileSystem(modDirectory), new ScriptConstants(), new LevelScriptHandler(data));
        reader.Read(Path.GetFileName(initCon));
        return data.Game;
    }

    /// <summary>
    /// The level folder an archive actually contains, compared against the map name it is filed
    /// under. Archives that hold no <c>bf1942/levels/...</c> entries at all (an objects archive,
    /// say) are not mismatched.
    /// </summary>
    private static bool HasMismatchedInternalName(RefractorFlatArchive archive, string mapNameRaw)
    {
        foreach (var entry in archive.Entries)
        {
            var segments = BfPath.Normalize(entry.Name).Split('/');
            if (segments.Length < 3 || !segments[0].Equals("bf1942", StringComparison.OrdinalIgnoreCase))
                return false;

            return !segments[2].Equals(mapNameRaw, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
