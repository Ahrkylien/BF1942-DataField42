namespace DataField42.Website.Data.Entities;

/// <summary>
/// Joins a <see cref="Tag"/> to a <see cref="Map"/>.
/// </summary>
public class TagLink
{
    public int Id { get; set; }

    public int? MapId { get; set; }

    public int? TagId { get; set; }

    public Map? Map { get; set; }

    public Tag? Tag { get; set; }
}
