using System.Buffers.Binary;
using System.Text;

namespace BF1942.FileSystem.Rfa;

public sealed class RefractorFlatArchive
{
    private const int ChunkSize = 32768;

    /// <summary>
    /// The four zero bytes the game's own tools leave after the table of contents.
    /// </summary>
    private const int TrailerLength = 4;

    /// <summary>
    /// What an entry takes in the table of contents when its name is empty: the length of the name
    /// and the six numbers after it.
    /// </summary>
    private const int MinimumEntrySize = 4 + 24;

    private static readonly HashSet<string> ClientOnlyExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".bik", ".dds", ".tga", ".wav" };

    private static readonly HashSet<string> ClientOnlyFileNames = new(StringComparer.OrdinalIgnoreCase)
        { "palette.pal", "envmap_g_.rcm", "lightmapshadowbits.lsb", "terrainpalette.pal", "textureprecache.dat" };

    private static ReadOnlySpan<byte> V11Signature => "Refractor2 FlatArchive 1.1  "u8;

    private readonly string _path;

    private FileTable? _table;

    /// <summary>
    /// An archive on disk. Nothing is read until the file list is needed.
    /// </summary>
    public RefractorFlatArchive(string path)
    {
        _path = path;
    }

    /// <summary>
    /// The files in the archive. The file list is read when this is first used, and that fails if the
    /// file is missing, is not an archive, or is cut off. <see cref="ReadFileListSafely"/> reads what
    /// a cut-off archive still holds.
    /// </summary>
    public IReadOnlyList<RefractorFlatArchiveEntry> Entries => Table.Entries;

    public bool IsCompressed => Table.IsCompressed;

    public bool IsV11Format => Table.IsV11;

    public XPackId XPackId => Table.XPackId;

    private FileTable Table => _table ??= ReadHeaderAndFileList(allowCutOff: false);

    /// <summary>
    /// Reads the file list, and reports what is wrong with the file instead of failing on it. An
    /// archive whose table of contents is cut off gives the files before the cut, which are then in
    /// <see cref="Entries"/>; the ones after it are lost, though their data is still in the file.
    /// </summary>
    /// <returns>
    /// What is wrong with the size of the file, as sentences that follow its name. The table of contents
    /// says how many files there are and where their data lies, and the file has to be as long as that.
    /// Empty when the size is right.
    /// </returns>
    /// <exception cref="IOException">The file is missing or is not an archive.</exception>
    public IReadOnlyList<string> ReadFileListSafely()
    {
        var table = _table ??= ReadHeaderAndFileList(allowCutOff: true);
        var problems = new List<string>();

        if (table.Entries.Count < table.DeclaredEntryCount)
            problems.Add($"is cut off: its table of contents lists {table.DeclaredEntryCount} files, but the file ends after {table.Entries.Count} of them");
        else if (table.FileLength - table.TableEnd is var extra && extra > TrailerLength)
            problems.Add($"has {extra} bytes after its table of contents");

        int beyondEnd = table.Entries.Count(entry => entry.Offset + entry.BlockSize > table.FileLength);
        if (beyondEnd > 0)
            problems.Add($"has {beyondEnd} files whose data lies beyond the end of the file");

        return problems;
    }

    public byte[] Read(RefractorFlatArchiveEntry e)
    {
        return DecodeRegion(ReadRegionFromFile(e), e.UncompressedSize, e.Name);
    }

    public List<(string Name, byte[] Data)> ReadServerEntries()
        => Entries
            .Where(e => !IsClientOnlyEntry(e.Name))
            .Select(e => (e.Name, Read(e)))
            .ToList();

    public static bool IsClientOnlyEntry(string entryName)
    {
        string ext = Path.GetExtension(entryName);
        string file = Path.GetFileName(entryName);
        return ClientOnlyExtensions.Contains(ext) || ClientOnlyFileNames.Contains(file);
    }

    public static void WriteFile(
        string path,
        IReadOnlyList<(string Name, byte[] Data)> entries,
        bool compress,
        XPackId xPackId)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        StreamArchive(
            fs,
            entries.Count,
            i => entries[i].Name,
            i => { var d = entries[i].Data; return (BuildRegion(d, compress), d.Length); },
            compress,
            xPackId);
    }

    public static void RepackToFile(
        string path,
        RefractorFlatArchive original,
        IReadOnlyDictionary<string, byte[]> replacements)
    {
        var ci = new Dictionary<string, byte[]>(replacements, StringComparer.OrdinalIgnoreCase);
        var ents = original.Entries;

        string tmp = path + ".rfatmp";
        try
        {
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                StreamArchive(
                    fs,
                    ents.Count,
                    i => ents[i].Name,
                    i => ci.TryGetValue(ents[i].Name, out var rep)
                        ? (BuildRegion(rep, original.IsCompressed), rep.Length)
                        : (original.RawRegion(ents[i]), ents[i].UncompressedSize),
                    original.IsCompressed,
                    original.XPackId
                );
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            try
            { File.Delete(tmp); }
            catch { }
            throw;
        }
    }

    private FileTable ReadHeaderAndFileList(bool allowCutOff)
    {
        using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadHeaderAndFileList(fs, allowCutOff);
    }

    private byte[] RawRegion(RefractorFlatArchiveEntry e)
    {
        return ReadRegionFromFile(e);
    }

    private byte[] ReadRegionFromFile(RefractorFlatArchiveEntry e)
    {
        using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(e.Offset, SeekOrigin.Begin);
        var buf = new byte[e.BlockSize];
        fs.ReadExactly(buf);
        return buf;
    }

    private static FileTable ReadHeaderAndFileList(Stream s, bool allowCutOff)
    {
        Span<byte> sig = stackalloc byte[28];

        s.ReadExactly(sig);
        bool isV11 = sig.SequenceEqual(V11Signature);
        if (!isV11)
            s.Seek(0, SeekOrigin.Begin);

        uint tocOffset = ReadU32(s);
        bool compressed = ReadU32(s) == 1;

        XPackId xpackId = XPackId.Default;
        if (!isV11)
        {
            Span<byte> desc = stackalloc byte[143];
            s.ReadExactly(desc);
            s.ReadByte();
            uint sum = 0;
            foreach (var b in desc)
                sum += b;
            xpackId = (XPackId)(ReadU32(s) - sum);
        }

        s.Seek(tocOffset, SeekOrigin.Begin);
        uint count = ReadU32(s);

        var list = new List<RefractorFlatArchiveEntry>((int)Math.Min(count, (s.Length - s.Position) / MinimumEntrySize));
        long tableEnd = s.Position;
        for (uint i = 0; i < count; i++)
        {
            var entry = ReadEntry(s);
            if (entry is null)
            {
                // The entries before the cut are still good.
                if (!allowCutOff)
                    throw new InvalidDataException($"The table of contents lists {count} files, but the file ends after {list.Count} of them");
                break;
            }

            list.Add(entry);
            tableEnd = s.Position;
        }

        return new FileTable(isV11, compressed, xpackId, list, count, tableEnd, s.Length);
    }

    /// <summary>
    /// One entry of the table of contents, or <see langword="null"/> when the file ends before it does.
    /// </summary>
    private static RefractorFlatArchiveEntry? ReadEntry(Stream s)
    {
        Span<byte> rec = stackalloc byte[24];

        if (s.Length - s.Position < sizeof(uint))
            return null;

        uint nameLength = ReadU32(s);
        if (s.Length - s.Position < nameLength + rec.Length)
            return null;

        var nameBytes = new byte[nameLength];
        s.ReadExactly(nameBytes);
        s.ReadExactly(rec);
        return new RefractorFlatArchiveEntry(
            Name: Encoding.Latin1.GetString(nameBytes),
            BlockSize: (int)BinaryPrimitives.ReadUInt32LittleEndian(rec),
            UncompressedSize: (int)BinaryPrimitives.ReadUInt32LittleEndian(rec.Slice(4)),
            Offset: BinaryPrimitives.ReadUInt32LittleEndian(rec.Slice(8)));
    }

    private static byte[] DecodeRegion(ReadOnlySpan<byte> region, int uncompressedSize, string? name = null)
    {
        if (region.Length == uncompressedSize)
            return region.ToArray();

        int numBlocks = (int)ReadU32(region, 0);
        int descBase = 4;
        int dataStart = descBase + numBlocks * 12;

        var result = new byte[uncompressedSize];
        int written = 0;
        for (int i = 0; i < numBlocks; i++)
        {
            int b = descBase + i * 12;
            int comp = (int)ReadU32(region, b);
            int unc = (int)ReadU32(region, b + 4);
            int cum = (int)ReadU32(region, b + 8);

            if (unc == 0)
                continue;

            var src = region.Slice(dataStart + cum, comp);
            var dst = result.AsSpan(written, unc);
            if (comp == unc)
                src.CopyTo(dst);
            else
            {
                var dstArray = MiniLZO.MiniLZO.Decompress(src.ToArray(), unc);
                dstArray.CopyTo(dst);
            }
            written += unc;
        }
        if (written != uncompressedSize)
            throw new InvalidDataException($"'{name}': reassembled {written} bytes, expected {uncompressedSize}.");
        return result;
    }

    private static byte[] BuildRegion(byte[] data, bool compress)
    {
        if (!compress)
            return data;

        int n = data.Length;
        int numBlocks = n == 0 ? 0 : (n + ChunkSize - 1) / ChunkSize;

        var comps = new List<byte[]>(numBlocks);
        var uncs = new int[numBlocks];
        for (int i = 0; i < numBlocks; i++)
        {
            int start = i * ChunkSize;
            int len = Math.Min(ChunkSize, n - start);
            var chunk = data.AsSpan(start, len);
            var lzo = MiniLZO.MiniLZO.Compress(chunk.ToArray());
            comps.Add(lzo.Length < len ? lzo : chunk.ToArray());
            uncs[i] = len;
        }

        using var ms = new MemoryStream();
        WriteU32(ms, (uint)numBlocks);
        int cum = 0;
        for (int i = 0; i < numBlocks; i++)
        {
            WriteU32(ms, (uint)comps[i].Length);
            WriteU32(ms, (uint)uncs[i]);
            WriteU32(ms, (uint)cum);
            cum += comps[i].Length;
        }
        foreach (var c in comps)
            ms.Write(c, 0, c.Length);
        return ms.ToArray();
    }

    private static void StreamArchive(
        Stream output,
        int count,
        Func<int, string> name,
        Func<int, (byte[] Region, int Unc)> getRegion,
        bool compress,
        XPackId xPackId)
    {
        if (!output.CanSeek)
            throw new ArgumentException("Writing an RFA requires a seekable stream (tocOffset is back-patched).", nameof(output));

        long start = output.Position;

        WriteU32(output, 0);
        WriteU32(output, compress ? 1u : 0u);

        byte[] descriptor = new byte[143];
        "RefractorForge"u8.CopyTo(descriptor);
        output.Write(descriptor, 0, 143);
        output.WriteByte(0);

        uint descriptorSum = 0;
        foreach (var b in descriptor)
            descriptorSum += b;
        WriteU32(output, (uint)xPackId + descriptorSum);

        var offsets = new long[count];
        var blockSizes = new int[count];
        var names = new byte[count][];
        var uncs = new int[count];
        for (int i = 0; i < count; i++)
        {
            offsets[i] = output.Position - start;
            var (region, unc) = getRegion(i);
            output.Write(region, 0, region.Length);
            blockSizes[i] = region.Length;
            names[i] = Encoding.Latin1.GetBytes(name(i));
            uncs[i] = unc;
        }

        long tocOffset = output.Position - start;
        if (tocOffset > uint.MaxValue)
            throw new NotSupportedException(
                $"Archive is {tocOffset:N0} bytes — the u32 container offsets cap it at {uint.MaxValue:N0} bytes.");

        WriteU32(output, (uint)count);
        for (int i = 0; i < count; i++)
        {
            WriteU32(output, (uint)names[i].Length);
            output.Write(names[i], 0, names[i].Length);
            WriteU32(output, (uint)blockSizes[i]);
            WriteU32(output, (uint)uncs[i]);
            WriteU32(output, (uint)offsets[i]);
            WriteU32(output, 0);
            WriteU32(output, 0);
            WriteU32(output, 0);
        }

        long end = output.Position;
        output.Position = start;
        WriteU32(output, (uint)tocOffset);
        output.Position = end;
    }

    private static uint ReadU32(Stream s)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        s.ReadExactly(bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    private static uint ReadU32(ReadOnlySpan<byte> d, int p) => BinaryPrimitives.ReadUInt32LittleEndian(d.Slice(p));

    private static void WriteU32(Stream s, uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        s.Write(b);
    }

    /// <summary>
    /// What the header and table of contents say about the archive, and how long the file is.
    /// </summary>
    private sealed record FileTable(
        bool IsV11,
        bool IsCompressed,
        XPackId XPackId,
        List<RefractorFlatArchiveEntry> Entries,
        uint DeclaredEntryCount,
        long TableEnd,
        long FileLength
    );
}
