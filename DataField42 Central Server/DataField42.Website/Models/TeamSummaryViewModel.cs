namespace DataField42.Website.Models;

/// <summary>
/// One team's totals on a scoreboard.
/// </summary>
public sealed class TeamSummaryViewModel
{
    public required int Number { get; init; }

    public int Score { get; init; }

    public uint Kills { get; init; }

    public uint Deaths { get; init; }

    public uint Ping { get; init; }

    public int PlayerCount { get; init; }

    /// <summary>
    /// How many players this team has fewer than the other, so both scoreboards can be padded to
    /// the same height.
    /// </summary>
    public int PlayerShortfall { get; init; }
}
