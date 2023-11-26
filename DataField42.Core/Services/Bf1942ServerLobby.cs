using System.Net.Http;
using System.Text.Json;

public class Bf1942ServerLobby
{
    public List<Bf1942Server> Servers { get; private set; } = new();
    private string MasterApiEntryPoint = "http://master.bf1942.org/json";

    public async Task QueryAllServers()
    {
        var queryTasks = new List<Task>();
        foreach (var server in Servers)
            queryTasks.Add(server.QueryServer());
        try
        {
            await Task.WhenAll(queryTasks);
        }
        catch (Exception ex)
        {
            // swallow exceptions from queries
        }
        
    }

    public async Task GetServerListFromHttpApi()
    {
        List<Bf1942Server> servers = new();
        try
        {
            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(MasterApiEntryPoint);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();

                // Deserialize JSON data

                List<List<object>>? jsonData = JsonSerializer.Deserialize<List<List<object>>>(json);

                if (jsonData == null || jsonData.Count == 0)
                    throw new Exception($"Failed to retrieve data from Master Api. Data: {jsonData} Count: {jsonData?.Count ?? 0}");

                // Process the retrieved data
                foreach (var item in jsonData)
                    servers.Add(new Bf1942Server(item[0].ToString() ?? "", int.Parse(item[1].ToString() ?? "")));
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
        Servers = servers;
    }
}