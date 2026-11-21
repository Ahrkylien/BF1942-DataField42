namespace DataField42.Website.Services.Tracking;

/// <summary>
/// What a server reported about itself in one status query, reduced to what the tracker records.
/// </summary>
/// <param name="HostPort">The port players connect to, which together with the address identifies the server.</param>
/// <param name="HostName">The name the server announces.</param>
/// <param name="MapName">The map being played.</param>
/// <param name="ModName">The mod being played, as the server's map id names it.</param>
/// <param name="GameType">The gameplay mode, such as conquest.</param>
/// <param name="PlayerCount">
/// The number of players as the master server reports it, which can be adjusted for servers that
/// pad their count.
/// </param>
/// <param name="PlayerCountNew">The real number of players, when the master server reports one.</param>
/// <param name="RoundTime">The length of the round, in seconds.</param>
/// <param name="RoundTimeRemaining">The time left in the round, in seconds.</param>
/// <param name="Tickets1">The tickets team 1 has left.</param>
/// <param name="Tickets2">The tickets team 2 has left.</param>
/// <param name="GameStartDelay">The delay before a round starts, in seconds.</param>
/// <param name="PlayersJson">The scoreboard as the JSON array the master server published, or <see langword="null"/> when nobody was playing.</param>
public sealed record ServerSnapshot(
    int HostPort,
    string? HostName,
    string MapName,
    string ModName,
    string GameType,
    int PlayerCount,
    int? PlayerCountNew,
    int RoundTime,
    int? RoundTimeRemaining,
    int? Tickets1,
    int? Tickets2,
    int GameStartDelay,
    string? PlayersJson)
{
    /// <summary>
    /// The number of players actually on the server: the real count when there is one, and the
    /// reported one otherwise.
    /// </summary>
    public int RealPlayerCount => PlayerCountNew ?? PlayerCount;
}
