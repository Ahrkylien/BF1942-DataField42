using System.Buffers.Binary;
using System.Text;

namespace BF1942.FileSystem.Rfa;

public sealed class RefractorFlatArchive
{
    private const int ChunkSize = 32768;

    private static ReadOnlySpan<byte> V11Signature
        => "Refractor2 FlatArchive 1.1  "u8;

    private readonly string? _path;

    public IReadOnlyList<RefractorFlatArchiveEntry> Entries { get; }

    public bool IsCompressed { get; }

    public bool IsV11Format { get; }

    public XPackId XPackId { get; }

    public RefractorFlatArchive(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var (isV11, isCompressed, xpackId, entries) = ReadFrom(fs);
        _path = path;
        Entries = entries;
        IsCompressed = isCompressed;
        IsV11Format = isV11;
        XPackId = xpackId;
    }

    private static (bool IsV11, bool Compressed, XPackId XPackId, List<RefractorFlatArchiveEntry> Entries) ReadFrom(Stream s)
    {
        Span<byte> u4 = stackalloc byte[4];
        Span<byte> sig = stackalloc byte[28];

        s.ReadExactly(sig);
        bool isV11 = sig.SequenceEqual(V11Signature);
        if (!isV11)
            s.Seek(0, SeekOrigin.Begin);

        s.ReadExactly(u4);
        uint tocOffset = BinaryPrimitives.ReadUInt32LittleEndian(u4);
        s.ReadExactly(u4);
        bool compressed = BinaryPrimitives.ReadUInt32LittleEndian(u4) == 1;

        XPackId xpackId = XPackId.Default;
        if (!isV11)
        {
            Span<byte> desc = stackalloc byte[143];
            s.ReadExactly(desc);
            s.ReadByte();
            uint sum = 0;
            foreach (var b in desc)
                sum += b;
            s.ReadExactly(u4);
            xpackId = (XPackId)(BinaryPrimitives.ReadUInt32LittleEndian(u4) - sum);
        }

        s.Seek(tocOffset, SeekOrigin.Begin);
        s.ReadExactly(u4);
        int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(u4);

        var list = new List<RefractorFlatArchiveEntry>(count);
        Span<byte> rec = stackalloc byte[24];
        for (int i = 0; i < count; i++)
        {
            s.ReadExactly(u4);
            var nameBytes = new byte[(int)BinaryPrimitives.ReadUInt32LittleEndian(u4)];
            s.ReadExactly(nameBytes);
            s.ReadExactly(rec);
            list.Add(new RefractorFlatArchiveEntry(
                Name: Encoding.Latin1.GetString(nameBytes),
                BlockSize: (int)BinaryPrimitives.ReadUInt32LittleEndian(rec),
                UncompressedSize: (int)BinaryPrimitives.ReadUInt32LittleEndian(rec.Slice(4)),
                Offset: BinaryPrimitives.ReadUInt32LittleEndian(rec.Slice(8))));
        }

        return (isV11, compressed, xpackId, list);
    }

    public byte[] Read(RefractorFlatArchiveEntry e)
    {
        return DecodeRegion(ReadRegionFromFile(e), e.UncompressedSize, e.Name);
    }

    private byte[] RawRegion(RefractorFlatArchiveEntry e)
    {
        return ReadRegionFromFile(e);
    }

    private byte[] ReadRegionFromFile(RefractorFlatArchiveEntry e)
    {
        using var fs = new FileStream(_path!, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(e.Offset, SeekOrigin.Begin);
        var buf = new byte[e.BlockSize];
        fs.ReadExactly(buf);
        return buf;
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

    private static readonly HashSet<string> ClientOnlyExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".bik", ".dds", ".tga", ".wav" };

    private static readonly HashSet<string> ClientOnlyFileNames = new(StringComparer.OrdinalIgnoreCase)
        { "palette.pal", "envmap_g_.rcm", "lightmapshadowbits.lsb", "terrainpalette.pal", "textureprecache.dat" };

    public static bool IsClientOnlyEntry(string entryName)
    {
        string ext = Path.GetExtension(entryName);
        string file = Path.GetFileName(entryName);
        return ClientOnlyExtensions.Contains(ext) || ClientOnlyFileNames.Contains(file);
    }

    public List<(string Name, byte[] Data)> ReadServerEntries()
        => Entries
            .Where(e => !IsClientOnlyEntry(e.Name))
            .Select(e => (e.Name, Read(e)))
            .ToList();

    private static uint ReadU32(ReadOnlySpan<byte> d, int p)
        => BinaryPrimitives.ReadUInt32LittleEndian(d.Slice(p));

    private static void WriteU32(Stream s, uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        s.Write(b);
    }
}
