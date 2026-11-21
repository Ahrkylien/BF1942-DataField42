using DataField42.Website.Services.ServerQuery;

namespace DataField42.Website.Models;

/// <summary>
/// One team's half of a scoreboard: its players and its totals.
/// </summary>
public sealed record TeamScoreboardViewModel(TeamSummaryViewModel Team, IReadOnlyList<ScoreboardPlayer> Players);
