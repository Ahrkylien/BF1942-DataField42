public class Player(string name, string team, string score, string kills, string deaths, string ping, string keyHash = "")
{
    public string Name { get; init; } = name;
    public int Team { get; init; } = int.TryParse(team, out var t) ? t : 0;
    public int Score { get; init; } = int.TryParse(score, out var s) ? s : 0;
    public uint Kills { get; init; } = uint.TryParse(kills, out var k) ? k : 0;
    public uint Deaths { get; init; } = uint.TryParse(deaths, out var d) ? d : 0;
    public uint Ping { get; init; } = uint.TryParse(ping, out var p) ? p : 0;
    public string KeyHash { get; init; } = keyHash;

    public override string ToString() => Name;
}
