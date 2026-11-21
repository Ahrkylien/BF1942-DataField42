namespace DataField42.Website.Data.Entities;

/// <summary>
/// A map or mod author.
/// </summary>
public class Author
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public ICollection<Map> Maps { get; set; } = [];
}
