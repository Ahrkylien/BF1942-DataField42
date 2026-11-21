using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataField42.Website.Services.Tracking;

/// <summary>
/// Writes one look at the master server's list into the history: which servers exist, the rounds
/// they play, and how many players were on them.
/// </summary>
/// <remarks>
/// Times are stored in the local time of the machine this runs on, which is how the existing history
/// was written and how it is read back.
/// </remarks>
public sealed class TrackingRecorder(DataField42DbContext context, TimeProvider timeProvider, ILogger<TrackingRecorder> logger)
{
    public async Task RecordAsync(IReadOnlyList<TrackedServer> servers, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetLocalNow().DateTime;

        foreach (var server in servers)
        {
            try
            {
                await RecordServerAsync(server, now, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One server's data must not cost every other server its sample.
                logger.LogWarning(ex, "Could not record {Ip}:{Port}", server.Ip, server.Current.HostPort);
                context.ChangeTracker.Clear();
            }
        }

        await RecordSampleAsync(
            context.PlayerNumbers.Where(sample => sample.RoundId == null),
            now,
            servers.Sum(server => server.Current.PlayerCount),
            playersRaw: null,
            roundId: null,
            cancellationToken);
    }

    private async Task RecordServerAsync(TrackedServer tracked, DateTime now, CancellationToken cancellationToken)
    {
        var server = await FindOrCreateServerAsync(tracked, cancellationToken);
        var current = tracked.Current;

        if (tracked.RoundStartedAt is { } startedAt)
        {
            var round = await FindOrCreateRoundAsync(server, tracked, startedAt, cancellationToken);

            if ((round.HasHadPlayers ?? 0) == 0 && current.RealPlayerCount > 0)
            {
                round.HasHadPlayers = 1;
                await context.SaveChangesAsync(cancellationToken);
            }

            await RecordSampleAsync(
                context.PlayerNumbers.Where(sample => sample.Round != null && sample.Round.ServerId == server.Id),
                now,
                current.PlayerCount,
                current.PlayerCountNew,
                round.Id,
                cancellationToken);
        }

        if (tracked.LastFinished is { } finished)
            await FinishRoundAsync(server, finished, cancellationToken);
    }

    private async Task<Server> FindOrCreateServerAsync(TrackedServer tracked, CancellationToken cancellationToken)
    {
        int gamePort = tracked.Current.HostPort;
        var server = await context.Servers.FirstOrDefaultAsync(s => s.Ip == tracked.Ip && s.GamePort == gamePort, cancellationToken);

        if (server is null)
        {
            server = new Server { Ip = tracked.Ip, GamePort = gamePort, QueryPort = tracked.QueryPort, Name = tracked.Current.HostName };
            context.Servers.Add(server);
            await context.SaveChangesAsync(cancellationToken);
        }
        else if (server.QueryPort is null && tracked.QueryPort is not null)
        {
            server.QueryPort = tracked.QueryPort;
            await context.SaveChangesAsync(cancellationToken);
        }

        return server;
    }

    private async Task<Round> FindOrCreateRoundAsync(Server server, TrackedServer tracked, DateTime startedAt, CancellationToken cancellationToken)
    {
        var round = await context.Rounds.FirstOrDefaultAsync(r => r.ServerId == server.Id && r.StartTime == startedAt, cancellationToken);
        if (round is not null)
            return round;

        var current = tracked.Current;
        round = new Round
        {
            ServerIp = tracked.Ip,
            ServerGamePort = current.HostPort,
            ServerId = server.Id,
            StartTime = startedAt,
            MapName = current.MapName,
            ModName = current.ModName,
            GameplayMode = current.GameType,
            Team1StartTickets = tracked.StartTickets1,
            Team2StartTickets = tracked.StartTickets2,
            GameStartDelay = current.GameStartDelay,
            RoundTime = current.RoundTime,
        };
        context.Rounds.Add(round);
        await context.SaveChangesAsync(cancellationToken);
        return round;
    }

    /// <summary>
    /// Records the final figures of a round the server has completed, once. A round is finished
    /// when it has no winner yet, so seeing the same finished round again changes nothing.
    /// </summary>
    private async Task FinishRoundAsync(Server server, FinishedRound finished, CancellationToken cancellationToken)
    {
        var snapshot = finished.Snapshot;
        if (snapshot.Tickets1 is not { } tickets1 || snapshot.Tickets2 is not { } tickets2)
        {
            logger.LogDebug("The finished round of {Ip}:{Port} has no tickets; not recording it", server.Ip, server.GamePort);
            return;
        }

        var matches = await context.Rounds
            .Where(r => r.ServerId == server.Id && r.StartTime == finished.StartedAt && r.WinningTeamNumber == null)
            .OrderBy(r => r.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        // Nothing to finish, or a start time that does not name one round: leave it alone.
        if (matches.Count != 1)
            return;

        var round = matches[0];

        var before = await context.PlayerNumbers
            .Where(sample => sample.Round != null && sample.Round.ServerId == server.Id && sample.RoundId < round.Id)
            .OrderByDescending(sample => sample.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var during = await context.PlayerNumbers
            .Where(sample => sample.RoundId == round.Id)
            .OrderBy(sample => sample.Id)
            .ToListAsync(cancellationToken);

        int? playersBefore = before is null ? null : EffectivePlayers(before);

        round.EndTime = finished.QueriedAt;
        round.Team1EndTickets = tickets1;
        round.Team2EndTickets = tickets2;
        round.RoundTime = snapshot.RoundTime;
        round.RoundTimeRemaining = snapshot.RoundTimeRemaining;
        round.WinningTeamNumber = RoundStatistics.WinningTeam(tickets1, tickets2);
        round.PlayerStats = snapshot.PlayersJson;
        round.MaximumNumberOfPlayers = RoundStatistics.MaximumPlayers([.. during.Select(EffectivePlayers)], playersBefore, snapshot.RealPlayerCount);

        if (round.StartTime is { } startTime
            && RoundStatistics.AveragePlayers(startTime, finished.QueriedAt, playersBefore, [.. during.Where(sample => sample.Date is not null).Select(sample => (sample.Date!.Value, EffectivePlayers(sample)))]) is { } average)
            round.AverageNumberOfPlayers = (float)average;

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Records how many players there are, unless that is what the last two samples already say, in
    /// which case the latest is moved forward to now. A server that sits at the same number for an
    /// hour therefore costs two rows, the first and the latest, rather than sixty, and the two still
    /// say exactly when the number held.
    /// </summary>
    private async Task RecordSampleAsync(
        IQueryable<PlayerNumber> series,
        DateTime now,
        int players,
        int? playersRaw,
        int? roundId,
        CancellationToken cancellationToken)
    {
        var latest = await series.OrderByDescending(sample => sample.Id).Take(2).ToListAsync(cancellationToken);

        bool unchanged = latest.Count == 2 && latest.All(sample => sample.Players == players && EffectivePlayers(sample) == (playersRaw ?? players));

        if (unchanged)
            latest[0].Date = now;
        else
            context.PlayerNumbers.Add(new PlayerNumber { Date = now, Players = players, PlayersRaw = playersRaw, RoundId = roundId });

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The number of players a sample stands for: the real count when it recorded one.
    /// </summary>
    private static int EffectivePlayers(PlayerNumber sample) => sample.PlayersRaw ?? sample.Players ?? 0;
}
