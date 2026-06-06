using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

public class Bf1942ServerLobby
{
    public List<Bf1942Server> Servers { get; private set; } = new();

    private readonly string _masterApiEntryPoint = "http://master.bf1942.org/json";
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

    public async Task GetServerListFromHttpApi()
    {
        _logger.LogDebug($"Fetching server list from {_masterApiEntryPoint}.");
        try
        {
            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(_masterApiEntryPoint);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
                List<List<object>>? jsonData = JsonSerializer.Deserialize<List<List<object>>>(json, options);

                if (jsonData == null || jsonData.Count == 0)
                    throw new Exception($"Failed to retrieve data from Master Api. Data: {jsonData} Count: {jsonData?.Count ?? 0}");

                int added = 0;
                foreach (var item in jsonData)
                {
                    try
                    {
                        var server = new Bf1942Server(IPAddress.Parse(item[0].ToString() ?? ""), int.Parse(item[1].ToString() ?? ""));
                        if (!Servers.Contains(server))
                        {
                            Servers.Add(server);
                            added++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse server entry from master API response.");
                    }
                }

                _logger.LogInformation($"Server list fetched: {Servers.Count} total servers, {added} newly added.");
            }
            else
            {
                throw new Exception($"Failed to retrieve data from Master Api. Status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to retrieve data from Master Api. An error occurred: {ex.Message}");
        }
    }
}
