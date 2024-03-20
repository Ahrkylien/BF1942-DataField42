public class Bf1942Player
{
    public string Name { get; init; }
    public uint Id { get; init; }
    public Team Team { get; init; }
    public int Score { get; init; }
    public uint Kills { get; init; }
    public uint Deaths { get; init; }
    public uint Ping { get; init; }
    public string Ip { get; init; }
    public string KeyHash { get; init; }
    public string Guid { get; init; }

    public Bf1942Player(string name, string id, string team, string score, string kills, string deaths, string ping, string ip, string keyHash, string guid)
    {
        var teamAsEnum = team switch
        {
            "[unknown]" => Team.Unknown,
            "axis" => Team.One,
            "allied" => Team.Two,
            _ => throw new ArgumentException($"Team is not in a valid format: {team}"),
        };
        Name = name;
        Id = uint.Parse(id);
        Team = teamAsEnum;
        Score = int.Parse(score);
        Kills = uint.Parse(kills);
        Deaths = uint.Parse(deaths);
        Ping = uint.Parse(ping);
        Ip = ip;
        KeyHash = keyHash;
        Guid = guid;
    }

    public override string ToString() => $"{Name} #{Id} {Team} {Ip} {KeyHash}";
}