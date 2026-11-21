namespace DataField42.Website.Data.Entities;

/// <summary>
/// A map/mod combination servers have been seen playing that the file database has no
/// <see cref="Map"/> row for. Backed by the <c>MapNotInDB</c> view.
/// </summary>
public class MapNotInDatabase
{
    public string? MapName { get; set; }

    public string? ModName { get; set; }
}
