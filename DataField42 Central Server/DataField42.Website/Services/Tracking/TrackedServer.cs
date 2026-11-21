namespace DataField42.Website.Services.Tracking;

/// <summary>
/// One server on the master server's list, as the tracker needs it.
/// </summary>
/// <param name="Ip">The server's address.</param>
/// <param name="QueryPort">The port the server answers status queries on.</param>
/// <param name="Current">The latest status.</param>
/// <param name="RoundStartedAt">When the round in progress started, to the second, in local time, once the master server has seen it start.</param>
/// <param name="StartTickets1">The tickets team 1 started the round in progress with.</param>
/// <param name="StartTickets2">The tickets team 2 started the round in progress with.</param>
/// <param name="LastFinished">The last round the server completed, if the master server saw one.</param>
public sealed record TrackedServer(
    string Ip,
    int? QueryPort,
    ServerSnapshot Current,
    DateTime? RoundStartedAt,
    int? StartTickets1,
    int? StartTickets2,
    FinishedRound? LastFinished);
