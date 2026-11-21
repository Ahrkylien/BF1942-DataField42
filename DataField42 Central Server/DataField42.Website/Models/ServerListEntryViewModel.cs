namespace DataField42.Website.Models;

/// <summary>
/// One row of the server list.
/// </summary>
public sealed class ServerListEntryViewModel
{
    public required string Ip { get; init; }

    public required Bf1942QueryResult Status { get; init; }

    /// <summary>
    /// The number of players actually playing, which is what the server reported less the
    /// permanently connected bot or spectator some servers run.
    /// </summary>
    public required int PlayerCount { get; init; }

    /// <summary>
    /// The port to link to: the game port when the server is in the database, the query port
    /// otherwise.
    /// </summary>
    public required int LinkPort { get; init; }

    /// <summary>
    /// Whether the server is tracked, which decides whether its link leads to a round history.
    /// </summary>
    public bool IsTracked { get; init; }

    /// <summary>
    /// How long ago the master server last heard from it.
    /// </summary>
    public string? QueriedAgo { get; init; }

    /// <summary>
    /// How long ago it last announced itself, for servers running DataField42.
    /// </summary>
    public string? HeartbeatAgo { get; init; }

    public string? Source { get; init; }

    public bool IsHeartbeatSource => string.Equals(Source, "Heartbeat", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The map name, title-cased for display.
    /// </summary>
    public string MapNameFormatted { get; init; } = string.Empty;
}
