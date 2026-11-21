namespace DataField42.Website.Services;

/// <summary>
/// How full one filesystem is. Sizes are in kibibytes, as <c>df</c> reports them.
/// </summary>
public sealed class DiskSpace
{
    public required string Name { get; init; }

    public long TotalSize { get; init; }

    public long UsedSize { get; init; }

    public long AvailableSize => TotalSize - UsedSize;

    public double FractionUsed => TotalSize == 0 ? 0 : (double)UsedSize / TotalSize;
}
