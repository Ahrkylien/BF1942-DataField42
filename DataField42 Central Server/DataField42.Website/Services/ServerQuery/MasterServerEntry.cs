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

    public required Bf1942QueryResult Status { get; init; }
}
