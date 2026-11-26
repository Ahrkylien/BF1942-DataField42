using DataField42.Website.Services.ServerQuery;

namespace DataField42.Website.Models;

/// <summary>
/// The server page, which doubles as the page for one historical round: live it shows the current
/// scoreboard and the recent rounds, and for a past round it shows that round's final scoreboard.
/// </summary>
public sealed class ServerRoundPageViewModel
{
    /// <summary>
    /// Whether this is the server as it is now, rather than a round out of the history.
    /// </summary>
    public bool IsLive { get; init; }

    public DateTime? StartDateTime { get; init; }

    public string? Name { get; init; }

    public required string Ip { get; init; }

    public uint? GamePort { get; init; }

    public int QueryPort { get; init; }

    /// <summary>
    /// The settings and scoreboard, or null when the server did not answer.
    /// </summary>
    public Bf1942QueryResult? Status { get; init; }

    public IReadOnlyList<ScoreboardPlayer> Players { get; init; } = [];

    /// <summary>
    /// The mod being played. Present for a recorded round as well as a live server, unlike the
    /// rest of a server's settings, which are not recorded.
    /// </summary>
    public string? ModName { get; init; }

    /// <summary>
    /// Whether the file database has a mod of this name, which is when <see cref="ModName"/> can link to the mod's maps.
    /// </summary>
    public bool ModIsInDatabase { get; init; }

    public int? Tickets1 { get; init; }

    public int? Tickets2 { get; init; }

    public int? RoundTimeSeconds { get; init; }

    /// <summary>
    /// How much of the round is left, as a percentage, when the server reports both a round
    /// length and a remainder.
    /// </summary>
    public double? RoundTimePercentage { get; init; }

    public string MapNameFormatted { get; init; } = string.Empty;

    public TeamSummaryViewModel? Team1 { get; init; }

    public TeamSummaryViewModel? Team2 { get; init; }

    /// <summary>
    /// The map being played, when it is one the file database knows.
    /// </summary>
    public MapBannerViewModel? Map { get; init; }

    public int? MapId { get; init; }

    public IReadOnlyList<RoundListItemViewModel> Rounds { get; init; } = [];

    public int NumberOfRoundsListed { get; init; }

    public LineGraphViewModel? PlayerNumbers { get; init; }
}
