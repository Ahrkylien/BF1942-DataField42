namespace BF1942.FileSystem.Rfa;

public sealed record RefractorFlatArchiveEntry(
    string Name,
    int BlockSize,
    int UncompressedSize,
    uint Offset)
{
    public override string ToString() => $"{Name} ({UncompressedSize:N0} B)";
}
