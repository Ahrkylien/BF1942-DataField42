namespace DataField42.Website.Models;

/// <summary>
/// Maps missing from the file database, grouped by mod.
/// </summary>
public sealed class MissingMapsPageViewModel
{
    public IReadOnlyList<(string Mod, IReadOnlyList<MissingMapViewModel> Maps)> MapsPerMod { get; init; } = [];
}
