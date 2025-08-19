using System.Net;
using System.Net.Http;
using System.Text.Json;

public class Bf1942ServerLobby
{
    public List<Bf1942Server> Servers { get; private set; } = new();
    private readonly string _masterApiEntryPoint = "http://master.bf1942.org/json";

    public async Task QueryAllServers()
    {
        var queryTasks = new List<Task>();
        foreach (var server in Servers)
            queryTasks.Add(server.QueryServer());
        try
        {
            await Task.WhenAll(queryTasks);
        }
        catch (Exception)
        {
            // swallow exceptions from queries
        }
        
    }

    public async Task GetServerListFromHttpApi()
    {
        try
        {
            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(_masterApiEntryPoint);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();

                // Deserialize JSON data

                List<List<object>>? jsonData = JsonSerializer.Deserialize<List<List<object>>>(json);

                if (jsonData == null || jsonData.Count == 0)
                    throw new Exception($"Failed to retrieve data from Master Api. Data: {jsonData} Count: {jsonData?.Count ?? 0}");

                // Process the retrieved data
                foreach (var item in jsonData)
                {
                    try
                    {
                        var server = new Bf1942Server(IPAddress.Parse(item[0].ToString() ?? ""), int.Parse(item[1].ToString() ?? ""));
                        if (!Servers.Contains(server))
                            Servers.Add(server);
                    }
                    catch
                    {
                        // ignore for now
                    }
                }
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