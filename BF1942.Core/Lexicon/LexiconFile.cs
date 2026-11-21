using System.Buffers.Binary;
using System.Text;

namespace BF1942.Lexicon;

/// <summary>
/// A mod's <c>lexiconAll.dat</c> string table: the display names the game shows for control
/// points, vehicles and briefing texts, keyed by the identifier the <c>.con</c> scripts use.
/// </summary>
public sealed class LexiconFile
{
    private readonly Dictionary<string, string[]> _entries;

    private LexiconFile(Dictionary<string, string[]> entries) => _entries = entries;

    public static LexiconFile Empty { get; } = new(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));

    public int Count => _entries.Count;

    /// <summary>
    /// Reads a lexicon, or returns <see langword="null"/> if the file is missing or malformed --
    /// a mod without a lexicon is normal, not an error.
    /// </summary>
    public static LexiconFile? TryRead(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 8)
                return null;

            int entryCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            int columnCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
            if (entryCount <= 0 || columnCount <= 0)
                return null;

            // A 38-byte tail of unknown purpose follows the strings; splitting on NUL and
            // indexing by entry/column ignores it, as the reference implementation does.
            var strings = Encoding.Unicode.GetString(bytes, 8, bytes.Length - 8).Split('\0');

            var entries = new Dictionary<string, string[]>(entryCount, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entryCount; i++)
            {
                int keyIndex = i * columnCount;
                if (keyIndex + columnCount > strings.Length)
                    break;

                entries[strings[keyIndex]] = strings[(keyIndex + 1)..(keyIndex + columnCount)];
            }

            return new LexiconFile(entries);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or DecoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>
    /// The localized string for <paramref name="name"/>, or <paramref name="name"/> itself when
    /// the lexicon has no entry for it (the game falls back to the raw identifier the same way).
    /// </summary>
    public string? Resolve(string? name, int column = 0)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        if (_entries.TryGetValue(name, out var columns) && column < columns.Length)
            return columns[column];

        return name;
    }

    /// <summary>
    /// Overlays <paramref name="other"/> on top of this lexicon, so a mod's own strings win over
    /// those of the mods it depends on.
    /// </summary>
    public LexiconFile Overlay(LexiconFile other)
    {
        var merged = new Dictionary<string, string[]>(_entries, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in other._entries)
            merged[key] = value;
        return new LexiconFile(merged);
    }
}
