using System.Globalization;
using System.Text.Json;
using DataField42.Website.Configuration;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Services.Tracking;

/// <summary>
/// Reads the master server's list for the tracker. The master server publishes, besides each
/// server's latest status, when the round in progress started and the status the last finished
/// round ended with, which is what makes round history possible without querying servers here.
/// </summary>
public sealed class TrackerFeedReader(HttpClient httpClient, IOptions<WebsiteOptions> options, ILogger<TrackerFeedReader> logger)
{
    /// <summary>
    /// Every server on the list, or <see langword="null"/> when the list could not be read. An empty
    /// list is an answer -- nobody is playing -- and is not the same as no answer.
    /// </summary>
    public async Task<IReadOnlyList<TrackedServer>?> ReadAsync(CancellationToken cancellationToken)
    {
        string url = options.Value.MasterServerUrl;

        try
        {
            await using var stream = await httpClient.GetStreamAsync(url, cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            var servers = new List<TrackedServer>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (TryReadServer(element) is { } server)
                    servers.Add(server);
            }

            return servers;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not read the server list from {Url}", url);
            return null;
        }
    }

    private TrackedServer? TryReadServer(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || TrackerJson.String(element, "IP") is not { Length: > 0 } ip
            || TrackerJson.Object(element, "query") is not { } query
            || TryReadSnapshot(query) is not { } current)
        {
            logger.LogDebug("Skipping a list entry that does not report a full status");
            return null;
        }

        DateTime? roundStartedAt = null;
        int? startTickets1 = null;
        int? startTickets2 = null;
        FinishedRound? lastFinished = null;

        if (TrackerJson.Object(element, "extra") is { } extra)
        {
            if (TrackerJson.Double(extra, "start_round_timestamp") is { } started)
                roundStartedAt = TrackerJson.LocalTime(started);

            startTickets1 = TrackerJson.Int(extra, "start_tickets1");
            startTickets2 = TrackerJson.Int(extra, "start_tickets2");
            lastFinished = TryReadFinishedRound(extra);
        }

        return new TrackedServer(ip, TrackerJson.Int(element, "queryPort"), current, roundStartedAt, startTickets1, startTickets2, lastFinished);
    }

    private FinishedRound? TryReadFinishedRound(JsonElement extra)
    {
        if (TrackerJson.Object(extra, "last_finished_round") is not { } finished
            || TrackerJson.Double(finished, "start_round_timestamp") is not { } started
            || TrackerJson.Double(finished, "query_timestamp") is not { } queried
            || TrackerJson.Object(finished, "query") is not { } query
            || TryReadSnapshot(query) is not { } snapshot)
            return null;

        return new FinishedRound(TrackerJson.LocalTime(started), TrackerJson.LocalTime(queried), snapshot);
    }

    /// <summary>
    /// The status the tracker records, or <see langword="null"/> when the entry lacks what identifies
    /// a server and its round. A real server reports all of it.
    /// </summary>
    private static ServerSnapshot? TryReadSnapshot(JsonElement query)
    {
        if (TrackerJson.Int(query, "hostport") is not { } hostPort
            || TrackerJson.String(query, "mapname") is not { } mapName
            || TrackerJson.String(query, "mapId") is not { } modName
            || TrackerJson.String(query, "gametype") is not { } gameType
            || TrackerJson.Int(query, "numplayers") is not { } playerCount
            || TrackerJson.Int(query, "roundTime") is not { } roundTime)
            return null;

        string? playersJson = query.TryGetProperty("players", out var players)
            && players.ValueKind == JsonValueKind.Array
            && players.GetArrayLength() > 0
                ? players.GetRawText()
                : null;

        return new ServerSnapshot(
            hostPort,
            TrackerJson.String(query, "hostname"),
            mapName,
            modName,
            gameType,
            playerCount,
            TrackerJson.Int(query, "numplayers_new"),
            roundTime,
            TrackerJson.Int(query, "roundTimeRemain"),
            TrackerJson.Int(query, "tickets1"),
            TrackerJson.Int(query, "tickets2"),
            ParseSeconds(TrackerJson.String(query, "game_start_delay")),
            playersJson);
    }

    /// <summary>
    /// A duration the server reports as text with a unit, such as <c>120s</c>. A server that does not
    /// report one has no delay.
    /// </summary>
    private static int ParseSeconds(string? text) =>
        text is not null && int.TryParse(text.TrimEnd('s'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) ? seconds : 0;
}
