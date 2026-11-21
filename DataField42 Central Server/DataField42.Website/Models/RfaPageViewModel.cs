namespace DataField42.Website.Models;

/// <summary>
/// The archive browser's file listing.
/// </summary>
public sealed class RfaPageViewModel
{
    public required int RfaId { get; init; }

    public required string Path { get; init; }

    public IReadOnlyList<string> Files { get; init; } = [];
}
