using System.Net.Sockets;
using System.Net;
using System.Text;

public class Bf1942ServerQuery
{
    private readonly string _ip;
    private readonly int _port;

    public Bf1942ServerQuery(string ip, int port)
    {
        _ip = ip;
        _port = port;
    }

    public async Task<Bf1942QueryResult> Query(int timeoutInMs)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(timeoutInMs);

        Dictionary<string, string> properties = new();
        UdpClient udpClient = new();

        try
        {
            udpClient.Connect(IPAddress.Parse(_ip), _port);
            await udpClient.SendAsync(Encoding.UTF8.GetBytes("\\status\\"), cancellationTokenSource.Token);

            IPEndPoint RemoteIpEndPoint = new(IPAddress.Any, 0);
            bool receivedFinalPacket = false;
            uint expectedNumberOfPackets = 0;

            for (int i = 0; i < 20; i++) // max 20 packets
            {
                byte[] receiveBytes = (await udpClient.ReceiveAsync(cancellationTokenSource.Token)).Buffer;

                string dataString = Bf1942Encoding.Decode(receiveBytes);
                var dataList = dataString.Split("\\");

                for (int j = 0; j < dataList.Length / 2; j++)
                    properties[dataList[j * 2 + 1]] = dataList[j * 2 + 2];

                if (properties.ContainsKey("final"))
                {
                    receivedFinalPacket = true;
                    expectedNumberOfPackets = uint.Parse(properties["queryid"].Split('.')[1]);
                }

                if (receivedFinalPacket && i + 1 == expectedNumberOfPackets)
                    break;
            }
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException($"Querying server {_ip}:{_port} timed out");
        }
        finally
        {
            udpClient.Close();
        }

        return new Bf1942QueryResult(properties);
    }
}

public class Bf1942QueryResult
{
    public Dictionary<string, string> Properties { get; init; } = new();

    public string GameName { get; init; }
    public string GameVersion { get; init; }
    public string GameId { get; init; }
    public uint HostPort { get; init; }
    public string HostName { get; init; }
    public string MapName { get; init; }
    public uint NumberOfPlayers { get; init; }
    public uint MaximumNumberOfPlayers { get; init; }
    public bool HasPassword { get; init; }
    public string Mod { get; init; }

    public List<Player> Players { get; init; }

    public Bf1942QueryResult(Dictionary<string, string> properties)
    {
        GameName = properties["gamename"];
        GameVersion = properties["gamever"];
        GameId = properties["gameId"];
        HostPort = uint.Parse(properties["hostport"]);
        HostName = properties["hostname"];
        MapName = properties["mapname"];
        NumberOfPlayers = uint.Parse(properties["numplayers"]);
        MaximumNumberOfPlayers = uint.Parse(properties["maxplayers"]);
        HasPassword = properties["password"] == "1";
        Mod = properties["mapId"];

        Players = new();
        for (int i = 0; i < NumberOfPlayers; i++)
        {
            Players.Add(new Player(
                Bf1942Encoding.Decode(Bf1942Encoding.Encode(properties["playername_" + i]), applySmartEncodingDetection: true),
                properties["team_" + i],
                properties["score_" + i],
                properties["kills_" + i],
                properties["deaths_" + i],
                properties["ping_" + i],
                properties.ContainsKey("keyhash_" + i) ? properties["keyhash_" + i] : ""
                ));
        }

        // save cleaned up properties:
        foreach (var (key, value) in properties)
        {
            var keyLower = key.ToLower();
            if (!keyLower.StartsWith("playername_")
                && !keyLower.StartsWith("team_")
                && !keyLower.StartsWith("score_")
                && !keyLower.StartsWith("kills_")
                && !keyLower.StartsWith("deaths_")
                && !keyLower.StartsWith("ping_")
                && !keyLower.StartsWith("keyhash_")
                && keyLower != "final")
            {
                Properties[key] = value;
            }
        }
    }
}

public class Player
{
    public string Name { get; init; }
    public int Team { get; init; }
    public int Score { get; init; }
    public uint Kills { get; init; }
    public uint Deaths { get; init; }
    public uint Ping { get; init; }
    public string KeyHash { get; init; }

    public Player(string name, string team, string score, string kills, string deaths, string ping, string keyHash = "")
    {
        Name = name;
        Team = int.Parse(team);
        Score = int.Parse(score);
        Kills = uint.Parse(kills);
        Deaths = uint.Parse(deaths);
        Ping = uint.Parse(ping);
        KeyHash = keyHash;
    }

    public override string ToString() => $"{Name}";
}