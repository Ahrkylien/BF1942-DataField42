namespace DataField42.Website.Data.Entities;

/// <summary>
/// A label a map can carry, such as the league or event it was made for.
/// </summary>
public class Tag
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Color { get; set; }

    public string? ColorHover { get; set; }

    public ICollection<TagLink> TagLinks { get; set; } = [];
}
