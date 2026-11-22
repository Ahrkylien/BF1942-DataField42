using DataField42.Website.Configuration;
using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using DataField42.Website.Models;
using DataField42.Website.Services.ServerQuery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Services;

/// <summary>
/// Builds the server list the home and servers pages show, by matching what the master server
/// reports against the servers the tracker has a history for.
/// </summary>
public sealed class ServerListService(
    MasterServerClient masterServer,
    DataField42DbContext context,
    IOptions<WebsiteOptions> options)
{
    private readonly WebsiteOptions _options = options.Value;

    /// <summary>
    /// Every listed server, in the order the master server gave them.
    /// </summary>
    public async Task<List<ServerListEntryViewModel>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        var listed = await masterServer.GetServersAsync(cancellationToken);
        var tracked = await context.Servers.AsNoTracking().ToListAsync(cancellationToken);
        var now = DateTimeOffset.Now;

        var entries = new List<ServerListEntryViewModel>(listed.Count);
        foreach (var entry in listed)
        {
            // Only a server with a known query port can be queried directly, so one without is
            // treated as untracked and linked by its listed query port instead.
            var match = tracked.FirstOrDefault(s =>
                s.Ip == entry.Ip && s.GamePort == entry.Status.HostPort && s.QueryPort is not null);

            entries.Add(new ServerListEntryViewModel
            {
                Ip = entry.Ip,
                Status = entry.Status,
                PlayerCount = CorrectPlayerCount(entry.Ip, entry.ReportedPlayerCount ?? entry.Status.NumberOfPlayers),
                IsTracked = match is not null,
                LinkPort = (match is not null ? (int)entry.Status.HostPort : entry.QueryPort) ?? 0,
                QueriedAgo = entry.QueriedAt is { } queriedAt ? TextFormatting.Duration((now - queriedAt).TotalSeconds) : null,
                HeartbeatAgo = entry.HeartbeatAt is { } heartbeatAt ? TextFormatting.Duration((now - heartbeatAt).TotalSeconds) : null,
                Source = entry.Source,
                MapNameFormatted = TextFormatting.Title(entry.Status.MapName),
            });
        }

        return entries;
    }

    /// <summary>
    /// Subtracts the players a server always reports but never really has -- a permanently
    /// connected bot or spectator -- so its player count matches what people see in game.
    /// </summary>
    public int CorrectPlayerCount(string ip, uint reported)
    {
        int corrected = (int)reported;
        if (_options.PlayerCountCorrections.TryGetValue(ip, out int correction))
            corrected -= correction;

        return Math.Max(0, corrected);
    }

    /// <summary>
    /// The tracked server at an address, matched on its game port.
    /// </summary>
    public Task<Server?> FindTrackedServerAsync(string ip, int gamePort, CancellationToken cancellationToken = default) =>
        context.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Ip == ip && s.GamePort == gamePort, cancellationToken);
}
