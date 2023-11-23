using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Net.Http;
using System.Text.Json;

public class Bf1942ServerLobby
{
    public List<Bf1942Server> Servers { get; private set; } = new();
    private string MasterApiEntryPoint = "http://master.bf1942.org/json";

    public async Task GetFromMasterApi()
    {
        Servers = await GetServerListFromApi();
        foreach (var server in Servers)
            server.QueryServer();
    }

    private async Task<List<Bf1942Server>> GetServerListFromApi()
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
        return servers;
    }
}