namespace DataField42.Website.Data.Entities;

/// <summary>
/// One round played on a server, recorded by the tracker.
/// </summary>
public class Round
{
    public int Id { get; set; }

    public string? ServerIp { get; set; }

    public int? ServerGamePort { get; set; }

    public int? ServerId { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string? MapName { get; set; }

    public string? ModName { get; set; }

    public string? GameplayMode { get; set; }

    public int? Team1StartTickets { get; set; }

    public int? Team2StartTickets { get; set; }

    public int? GameStartDelay { get; set; }

    /// <summary>
    /// The round's time limit in seconds.
    /// </summary>
    public int? RoundTime { get; set; }

    public int? Team1EndTickets { get; set; }

    public int? Team2EndTickets { get; set; }

    public int? RoundTimeRemaining { get; set; }

    public int? WinningTeamNumber { get; set; }

    /// <summary>
    /// The scoreboard at the end of the round, as a JSON array of player objects.
    /// </summary>
    public string? PlayerStats { get; set; }

    public float? AverageNumberOfPlayers { get; set; }

    public int? MaximumNumberOfPlayers { get; set; }

    /// <summary>
    /// Whether anyone played: rounds that cycled on an empty server are not worth listing.
    /// </summary>
    public int? HasHadPlayers { get; set; }

    public Server? Server { get; set; }

    public ICollection<PlayerNumber> PlayerNumbers { get; set; } = [];
}
