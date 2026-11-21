namespace DataField42.Website.Data.Entities;

/// <summary>
/// A game server the tracker knows about.
/// </summary>
public class Server
{
    public int Id { get; set; }

    public string? Ip { get; set; }

    public int? GamePort { get; set; }

    /// <summary>
    /// The port the server answers status queries on, which is not derivable from
    /// <see cref="GamePort"/> for every server.
    /// </summary>
    public int? QueryPort { get; set; }

    public string? Name { get; set; }

    public ICollection<Round> Rounds { get; set; } = [];
}
