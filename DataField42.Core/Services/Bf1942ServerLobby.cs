using BF1942.Core.MasterServer;
using Microsoft.Extensions.Logging;
using System.Net;

public class Bf1942ServerLobby
{
    public List<Bf1942Server> Servers { get; private set; } = new();

    private const string MasterServerHost = "master.bf1942.org";

    private readonly Bf1942MasterServerClient _masterServer = new();
    private readonly ILogger<Bf1942ServerLobby> _logger;

    public Bf1942ServerLobby(ILogger<Bf1942ServerLobby> logger)
    {
        _logger = logger;
    }

    public async Task QueryAllServers()
    {
        _logger.LogDebug($"Querying all {Servers.Count} servers.");
        var queryTasks = new List<Task>();
        foreach (var server in Servers)
            queryTasks.Add(server.QueryServer(TimeSpan.FromSeconds(3)));
        try
        {
            await Task.WhenAll(queryTasks);
            _logger.LogDebug("All server queries completed.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "One or more server queries failed during QueryAllServers.");
        }
    }

    public Bf1942Server GetOrCreate(IPAddress ip, int port)
    {
        var existing = Servers.FirstOrDefault(s => s.Ip.Equals(ip) && s.QueryPort == port);
        if (existing != null)
            return existing;
        var server = new Bf1942Server(ip, port);
        Servers.Add(server);
        return server;
    }

    /// <summary>
    /// Adds the game servers the master server knows of, which it tells over its own TCP protocol.
    /// </summary>
    public async Task GetServerListFromMaster(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug($"Fetching server list from {MasterServerHost}.");

        var endpoints = await _masterServer.GetServerList(MasterServerHost, cancellationToken: cancellationToken);

        int added = 0;
        foreach (var endpoint in endpoints)
        {
            var server = new Bf1942Server(endpoint.Address, endpoint.Port);
            if (!Servers.Contains(server))
            {
                Servers.Add(server);
                added++;
            }
        }

        _logger.LogInformation($"Server list fetched: {Servers.Count} total servers, {added} newly added.");
    }
}
