namespace DataField42.Website.Services;

/// <summary>
/// One point of a player-count series.
/// </summary>
public readonly record struct PlayerCountSample(DateTime Date, int Players);
