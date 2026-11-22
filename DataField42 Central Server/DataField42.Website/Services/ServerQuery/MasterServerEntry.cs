namespace DataField42.Website.Services.ServerQuery;

/// <summary>
/// One server as the master server lists it.
/// </summary>
public sealed class MasterServerEntry
{
    public required string Ip { get; init; }

    public int? QueryPort { get; init; }

    /// <summary>
    /// When the master server last got an answer out of this server.
    /// </summary>
    public DateTimeOffset? QueriedAt { get; init; }

    /// <summary>
    /// When this server last announced itself, which only servers running DataField42 do.
    /// </summary>
    public DateTimeOffset? HeartbeatAt { get; init; }

    /// <summary>
    /// How the master server knows about it, as it reports it.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// The number of players the master server reports, which is the number to show. The scoreboard
    /// it publishes can hold fewer players than that, or none, for a server that does not send it
    /// in full, so <see cref="Status"/> only knows as many players as the scoreboard lists.
    /// </summary>
    public uint? ReportedPlayerCount { get; init; }

    public required Bf1942QueryResult Status { get; init; }
}
