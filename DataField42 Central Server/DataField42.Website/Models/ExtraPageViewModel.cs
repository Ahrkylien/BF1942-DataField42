using DataField42.Website.Services;

namespace DataField42.Website.Models;

/// <summary>
/// The extra page: links to the reference pages, how full the storage is, and the featured
/// server's average week.
/// </summary>
public sealed class ExtraPageViewModel
{
    public IReadOnlyList<DiskSpace> Filesystems { get; init; } = [];

    public LineGraphViewModel? PlayerNumbers { get; init; }
}
