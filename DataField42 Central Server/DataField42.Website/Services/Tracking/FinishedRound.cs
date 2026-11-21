namespace DataField42.Website.Services.Tracking;

/// <summary>
/// The last round a server completed, with the status it reported when that round ended.
/// </summary>
/// <param name="StartedAt">When the round started, to the second, in local time.</param>
/// <param name="QueriedAt">When the final status was read, which is taken as the end of the round.</param>
/// <param name="Snapshot">The final status.</param>
public sealed record FinishedRound(DateTime StartedAt, DateTime QueriedAt, ServerSnapshot Snapshot);
