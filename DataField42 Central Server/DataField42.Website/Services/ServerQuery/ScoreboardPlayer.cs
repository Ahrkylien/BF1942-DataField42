namespace DataField42.Website.Services.ServerQuery;

/// <summary>
/// A player as a scoreboard shows them: what the server reported, plus the ranking and the clan
/// tag split off their name, neither of which the server knows about.
/// </summary>
public sealed class ScoreboardPlayer
{
    public required Player Player { get; init; }

    /// <summary>
    /// The clan tag the name starts with, or empty when it carries none.
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    public string? ClanName { get; set; }

    /// <summary>
    /// The name with the clan tag removed, which is what the scoreboard prints after the tag.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Rank on the scoreboard, counting from one.
    /// </summary>
    public int Place { get; set; }

    public int Team => Player.Team;

    public int Score => Player.Score;

    public uint Kills => Player.Kills;

    public uint Deaths => Player.Deaths;

    public uint Ping => Player.Ping;

    /// <summary>
    /// Scoreboard order: score, then kills, then fewest deaths, then best ping.
    /// </summary>
    public static IComparer<ScoreboardPlayer> Order { get; } = Comparer<ScoreboardPlayer>.Create((a, b) =>
    {
        int result = b.Score.CompareTo(a.Score);
        if (result != 0)
            return result;
        result = b.Kills.CompareTo(a.Kills);
        if (result != 0)
            return result;
        result = a.Deaths.CompareTo(b.Deaths);
        return result != 0 ? result : b.Ping.CompareTo(a.Ping);
    });
}
