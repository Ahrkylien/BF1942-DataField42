namespace DataField42.Website.Data.Entities;

/// <summary>
/// A game modification, as a folder under the file database's <c>mods</c> directory.
/// </summary>
public class Mod
{
    public int Id { get; set; }

    /// <summary>
    /// The folder the mod lives in, which is also the name servers report as their <c>gameId</c>.
    /// </summary>
    public string? FolderName { get; set; }

    public string? FullName { get; set; }

    public string? Version { get; set; }

    public DateTime? LastUpdate { get; set; }

    public int? AuthorId { get; set; }

    /// <summary>
    /// The mod's <c>game.addModPath</c> declarations as a JSON array, e.g.
    /// <c>["Mods/BF1942/", "Mods/DC_Final/"]</c>.
    /// </summary>
    public string? ModDependencies { get; set; }

    public Author? Author { get; set; }

    public ICollection<Map> Maps { get; set; } = [];
}
