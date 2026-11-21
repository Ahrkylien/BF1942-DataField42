namespace DataField42.Website.Data.Entities;

/// <summary>
/// A sample of how many players were online at a moment in time. A sample with no
/// <see cref="RoundId"/> counts every tracked server at once.
/// </summary>
public class PlayerNumber
{
    public int Id { get; set; }

    public DateTime? Date { get; set; }

    public int? RoundId { get; set; }

    /// <summary>
    /// The player count after the corrections the tracker applies for servers known to report
    /// a permanently connected bot or spectator.
    /// </summary>
    public int? Players { get; set; }

    /// <summary>
    /// The count exactly as the server reported it.
    /// </summary>
    public int? PlayersRaw { get; set; }

    public Round? Round { get; set; }
}
