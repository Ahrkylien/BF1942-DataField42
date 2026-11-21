using DataField42.Website.Services;

namespace DataField42.Website.Services.ServerQuery;

/// <summary>
/// Builds the scoreboard a page shows from the players a server reported: clan tags split off the
/// names, and the players ranked.
/// </summary>
public sealed class Scoreboard(ClanTagService clanTags)
{
    public List<ScoreboardPlayer> Build(IEnumerable<Player>? players)
    {
        var scoreboard = new List<ScoreboardPlayer>();

        foreach (var player in players ?? [])
        {
            var entry = new ScoreboardPlayer { Player = player, Name = player.Name };

            if (clanTags.Match(player.Name) is { } match)
            {
                entry.Tag = match.Tag;
                entry.ClanName = match.ClanName;
                entry.Name = match.PlayerName;
            }

            scoreboard.Add(entry);
        }

        scoreboard.Sort(ScoreboardPlayer.Order);
        for (int i = 0; i < scoreboard.Count; i++)
            scoreboard[i].Place = i + 1;

        return scoreboard;
    }
}
