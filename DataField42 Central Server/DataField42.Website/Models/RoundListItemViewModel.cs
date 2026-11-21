namespace DataField42.Website.Models;

/// <summary>
/// One round in a server's history, or the date heading above a day's rounds.
/// </summary>
public sealed class RoundListItemViewModel
{
    /// <summary>
    /// Set on a heading row; null on an actual round.
    /// </summary>
    public string? DaySeparator { get; init; }

    public DateTime? StartTime { get; init; }

    public string StartTimeForDisplay { get; init; } = string.Empty;

    public string? MapName { get; init; }

    public string? GameplayModeShort { get; init; }

    public int? MaximumNumberOfPlayers { get; init; }

    /// <summary>
    /// The top scorer of the round.
    /// </summary>
    public string? BestPlayer { get; init; }

    public int? Team1StartTickets { get; init; }

    public int? Team2StartTickets { get; init; }

    public int? Team1EndTickets { get; init; }

    public int? Team2EndTickets { get; init; }

    public int? WinningTeamNumber { get; init; }
}
