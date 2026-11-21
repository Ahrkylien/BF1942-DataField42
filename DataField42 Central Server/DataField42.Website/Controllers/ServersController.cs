using System.Globalization;
using System.Net;
using System.Text.Json;
using DataField42.Website.Configuration;
using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using DataField42.Website.Models;
using DataField42.Website.Services;
using DataField42.Website.Services.ServerQuery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Controllers;

/// <summary>
/// The server list, the live page for one server, and the page for one round out of its history.
/// </summary>
public sealed class ServersController(
    DataField42DbContext context,
    ServerListService serverList,
    Scoreboard scoreboard,
    MapPresenter presenter,
    IOptions<WebsiteOptions> options,
    PlayerActivityStatistics statistics) : Controller
{
    private const int DefaultRoundCount = 20;

    private readonly WebsiteOptions _options = options.Value;

    private bool IsAdmin => User.Identity?.IsAuthenticated == true;

    [HttpGet("/Servers")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var servers = await serverList.GetServersAsync(cancellationToken);

        // A blank row separates the populated servers from the empty ones.
        var rows = new List<ServerListEntryViewModel?>(servers.Count + 1);
        bool separatorAdded = false;
        foreach (var server in servers)
        {
            if (!separatorAdded && server.PlayerCount == 0)
            {
                rows.Add(null);
                separatorAdded = true;
            }

            rows.Add(server);
        }

        var model = new ServersPageViewModel
        {
            TotalPlayers = servers.Sum(server => server.PlayerCount),
            ServerCount = servers.Count,
            Rows = rows,
        };

        ViewData["Title"] = "Servers";
        ViewData["ActiveNav"] = "Servers";
        return View(model);
    }

    /// <summary>
    /// A server by its game port, which is the address people share.
    /// </summary>
    [HttpGet("/Server/{ip}:{port:int}")]
    public async Task<IActionResult> Server(string ip, int port, [FromQuery] int? nrounds, CancellationToken cancellationToken)
    {
        var tracked = await serverList.FindTrackedServerAsync(ip, port, cancellationToken);
        if (tracked?.QueryPort is not { } queryPort)
            return Redirect("/Servers");

        return await ShowLiveAsync(ip, queryPort, tracked, nrounds, cancellationToken);
    }

    [HttpGet("/Server/FromID/{id:int}")]
    public async Task<IActionResult> ServerFromId(int id, [FromQuery] int? nrounds, CancellationToken cancellationToken)
    {
        var tracked = await context.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (tracked?.Ip is null || tracked.QueryPort is not { } queryPort)
            return Redirect("/Servers");

        return await ShowLiveAsync(tracked.Ip, queryPort, tracked, nrounds, cancellationToken);
    }

    /// <summary>
    /// A server by its query port, for servers that are listed but not tracked.
    /// </summary>
    [HttpGet("/Server/FromQueryPort/{ip}:{port:int}")]
    public Task<IActionResult> ServerFromQueryPort(string ip, int port, [FromQuery] int? nrounds, CancellationToken cancellationToken) =>
        ShowLiveAsync(ip, port, trackedServer: null, nrounds, cancellationToken);

    /// <summary>
    /// One round out of a server's history, addressed by the moment it started.
    /// </summary>
    [HttpGet("/Server/{ip}:{port:int}/Round/{startTime}")]
    public async Task<IActionResult> Round(string ip, int port, string startTime, CancellationToken cancellationToken)
    {
        if (!DateTime.TryParse(startTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            return Redirect("/Servers");

        var tracked = await serverList.FindTrackedServerAsync(ip, port, cancellationToken);
        if (tracked is null)
            return Redirect("/Servers");

        var round = await context.Rounds
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.ServerId == tracked.Id && r.StartTime == start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond)),
                cancellationToken);

        return await RenderRoundAsync(ip, port, tracked, round, start, cancellationToken);
    }

    private async Task<IActionResult> ShowLiveAsync(string ip, int queryPort, Server? trackedServer, int? roundCount, CancellationToken cancellationToken)
    {
        var status = await QueryAsync(ip, queryPort, cancellationToken);

        // A server listed under a different game port than the database records is matched by
        // what it just reported, so an address change does not lose its history.
        if (trackedServer is null && status is not null)
            trackedServer = await serverList.FindTrackedServerAsync(ip, (int)status.HostPort, cancellationToken);

        return await RenderAsync(ip, queryPort, trackedServer, status, isLive: true, startDateTime: null, roundCount ?? DefaultRoundCount, cancellationToken);
    }

    /// <summary>
    /// Queries one server directly. A server that does not answer in time, or answers something
    /// that cannot be read, leaves the page saying so rather than failing.
    /// </summary>
    private async Task<Bf1942QueryResult?> QueryAsync(string ip, int port, CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(ip, out var address))
            return null;

        try
        {
            return await new Bf1942ServerQuery(address, port).Query(_options.ServerQueryTimeout);
        }
        catch (Exception ex) when (ex is TimeoutException or ProtocolViolationException or System.Net.Sockets.SocketException)
        {
            return null;
        }
    }

    private async Task<IActionResult> RenderAsync(
        string ip,
        int queryPort,
        Server? trackedServer,
        Bf1942QueryResult? status,
        bool isLive,
        DateTime? startDateTime,
        int roundCount,
        CancellationToken cancellationToken)
    {
        var players = scoreboard.Build(status?.Players);
        var (team1, team2) = BuildTeamSummaries(players);
        var mapBanner = status is null ? null : await FindMapAsync(status.GameId, status.MapName, cancellationToken);

        var model = new ServerRoundPageViewModel
        {
            IsLive = isLive,
            StartDateTime = startDateTime,
            Name = string.IsNullOrEmpty(status?.HostName) ? trackedServer?.Name : status.HostName,
            Ip = ip,
            GamePort = status?.HostPort ?? (uint?)trackedServer?.GamePort,
            QueryPort = queryPort,
            Status = status,
            Players = players,
            ModName = status?.GameId,
            Tickets1 = status?.Tickets1,
            Tickets2 = status?.Tickets2,
            RoundTimeSeconds = status?.RoundTime,
            RoundTimePercentage = CalculateRoundTimePercentage(status),
            MapNameFormatted = mapBanner?.Name ?? TextFormatting.Title(status?.MapName),
            Team1 = status is null ? null : team1,
            Team2 = status is null ? null : team2,
            Map = mapBanner,
            MapId = mapBanner?.MapId,
            Rounds = isLive && trackedServer is not null
                ? await LoadRoundsAsync(trackedServer.Id, roundCount, cancellationToken)
                : [],
            NumberOfRoundsListed = roundCount,
            PlayerNumbers = isLive && trackedServer is not null
                ? await LoadGraphAsync(trackedServer.Id, cancellationToken)
                : null,
        };

        ViewData["Title"] = "Server";
        ViewData["ActiveNav"] = "Servers";
        ViewData["HasChartJs"] = true;
        return View("Round", model);
    }

    /// <summary>
    /// The page for one round out of the history. A recorded round is not a server status -- the
    /// tracker keeps the scoreboard and the result, not the several dozen settings a server
    /// reports -- so it is shown from what was recorded rather than dressed up as a query.
    /// </summary>
    private async Task<IActionResult> RenderRoundAsync(
        string ip,
        int port,
        Server trackedServer,
        Round? round,
        DateTime startDateTime,
        CancellationToken cancellationToken)
    {
        var players = scoreboard.Build(ReadRecordedPlayers(round?.PlayerStats));
        var (team1, team2) = BuildTeamSummaries(players);
        var map = round is null ? null : await FindMapAsync(round.ModName, round.MapName, cancellationToken);

        var model = new ServerRoundPageViewModel
        {
            IsLive = false,
            StartDateTime = startDateTime,
            Name = trackedServer.Name,
            Ip = ip,
            GamePort = (uint?)trackedServer.GamePort,
            QueryPort = port,
            Players = players,
            Team1 = round is null ? null : team1,
            Team2 = round is null ? null : team2,
            MapNameFormatted = map?.Name ?? TextFormatting.Title(round?.MapName),
            ModName = round?.ModName,
            Tickets1 = round?.Team1EndTickets,
            Tickets2 = round?.Team2EndTickets,
            RoundTimeSeconds = round?.RoundTime,
            Map = map,
            MapId = map?.MapId,
        };

        ViewData["Title"] = "Server";
        ViewData["ActiveNav"] = "Servers";
        ViewData["HasChartJs"] = true;
        return View("Round", model);
    }

    /// <summary>
    /// The scoreboard the tracker recorded for a round.
    /// </summary>
    private static List<Player> ReadRecordedPlayers(string? playerStats)
    {
        var players = new List<Player>();
        if (playerStats is null)
            return players;

        try
        {
            using var document = JsonDocument.Parse(playerStats);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return players;

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                players.Add(new Player(
                    Read(element, "playername"),
                    Read(element, "team"),
                    Read(element, "score"),
                    Read(element, "kills"),
                    Read(element, "deaths"),
                    Read(element, "ping"),
                    Read(element, "keyhash")));
            }
        }
        catch (JsonException)
        {
            // A round recorded by an older tracker may not parse; the rest of the page is still
            // worth showing.
        }

        return players;

        static string Read(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) ? value.ToString() : "";
    }

    private static (TeamSummaryViewModel Team1, TeamSummaryViewModel Team2) BuildTeamSummaries(IReadOnlyList<ScoreboardPlayer> players)
    {
        var team1 = players.Where(player => player.Team == 1).ToList();
        var team2 = players.Where(player => player.Team == 2).ToList();

        return (Summarize(1, team1, team2.Count), Summarize(2, team2, team1.Count));
    }

    private static TeamSummaryViewModel Summarize(int number, List<ScoreboardPlayer> players, int otherTeamCount) => new()
    {
        Number = number,
        Score = players.Sum(player => player.Score),
        Kills = (uint)players.Sum(player => player.Kills),
        Deaths = (uint)players.Sum(player => player.Deaths),
        Ping = players.Count == 0 ? 0 : (uint)Math.Round(players.Average(player => player.Ping)),
        PlayerCount = players.Count,
        PlayerShortfall = Math.Max(0, otherTeamCount - players.Count),
    };

    private static double? CalculateRoundTimePercentage(Bf1942QueryResult? status)
    {
        if (status is null || status.RoundTime == 0)
            return null;

        return (double)status.RoundTimeRemain / status.RoundTime * 100;
    }

    /// <summary>
    /// The map being played, when the mod and map name match something in the file database.
    /// </summary>
    private async Task<MapBannerViewModel?> FindMapAsync(string? modName, string? mapName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(modName) || string.IsNullOrEmpty(mapName))
            return null;

        var map = await context.Maps
            .AsNoTracking()
            .Include(m => m.Mod)
            .Include(m => m.Author)
            .FirstOrDefaultAsync(m => m.Mod!.FolderName == modName && m.Name == mapName, cancellationToken);

        if (map is null)
            return null;

        var tags = await presenter.GetTagsAsync([map.Id], cancellationToken);
        return presenter.CreateBanner(map, tags.GetValueOrDefault(map.Id, []), IsAdmin);
    }

    /// <summary>
    /// The most recent rounds that had players, with a heading row inserted above each day.
    /// </summary>
    private async Task<List<RoundListItemViewModel>> LoadRoundsAsync(int serverId, int roundCount, CancellationToken cancellationToken)
    {
        // The newest round is the one in progress, which has no result yet, so it is skipped.
        var rounds = await context.Rounds
            .AsNoTracking()
            .Where(round => round.ServerId == serverId && round.HasHadPlayers == 1)
            .OrderByDescending(round => round.StartTime)
            .Take(roundCount + 1)
            .ToListAsync(cancellationToken);

        if (rounds.Count > 0)
            rounds.RemoveAt(0);

        var today = DateTime.Today;
        var items = new List<RoundListItemViewModel>(rounds.Count);
        DateTime? lastDate = null;

        foreach (var round in rounds)
        {
            var date = round.StartTime?.Date;
            if (date != lastDate)
            {
                items.Add(new RoundListItemViewModel { DaySeparator = FormatDayHeading(date, today) });
                lastDate = date;
            }

            items.Add(new RoundListItemViewModel
            {
                StartTime = round.StartTime,
                StartTimeForDisplay = round.StartTime?.ToString("HH:mm") ?? string.Empty,
                MapName = TextFormatting.Title(round.MapName),
                GameplayModeShort = TextFormatting.GameplayModeShort(round.GameplayMode),
                MaximumNumberOfPlayers = round.MaximumNumberOfPlayers,
                BestPlayer = FindBestPlayer(round.PlayerStats),
                Team1StartTickets = round.Team1StartTickets,
                Team2StartTickets = round.Team2StartTickets,
                Team1EndTickets = round.Team1EndTickets,
                Team2EndTickets = round.Team2EndTickets,
                WinningTeamNumber = round.WinningTeamNumber,
            });
        }

        return items;
    }

    private static string FormatDayHeading(DateTime? date, DateTime today)
    {
        if (date is null)
            return string.Empty;
        if (date == today)
            return "Today";
        if (date == today.AddDays(-1))
            return "Yesterday";

        return date.Value.ToString("dddd dd MMMM yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The top scorer of a recorded round.
    /// </summary>
    private string? FindBestPlayer(string? playerStats)
    {
        var players = scoreboard.Build(ReadRecordedPlayers(playerStats));
        return players.Count == 0 ? null : players[0].Name;
    }

    /// <summary>
    /// The last day of player counts for one server, with each map change marked.
    /// </summary>
    private async Task<LineGraphViewModel> LoadGraphAsync(int serverId, CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromDays(1);
        var from = DateTime.Now - window;

        var annotations = await context.Rounds
            .AsNoTracking()
            .Where(round => round.ServerId == serverId && round.StartTime > from && round.StartTime <= DateTime.Now)
            .OrderBy(round => round.StartTime)
            .Select(round => new { round.MapName, round.StartTime })
            .ToListAsync(cancellationToken);

        return new LineGraphViewModel
        {
            Samples = await statistics.GetSeriesAsync(serverId, window, cancellationToken),
            Annotations =
            [
                .. annotations
                    .Where(round => round.StartTime.HasValue)
                    .Select(round => new GraphAnnotation(
                        TextFormatting.Title(round.MapName),
                        LineGraphViewModel.FormatTimestamp(round.StartTime!.Value)))
            ],
        };
    }
}
