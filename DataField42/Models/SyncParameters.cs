namespace DataField42;

public class SyncParameters
{
    public string Mod;
    public string Map;
    public string Ip;
    public int Port;
    public string KeyHash;
    public string Password;

    public SyncParameters(string mod, string map, string hostName, int port, string keyHash, string password)
    {
        Mod = mod;
        Map = map;
        Ip = hostName;
        Port = port;
        KeyHash = keyHash;
        Password = password;
    }

    public override string ToString() => $"SyncAndJoinServer \"{Mod}\" \"{Map}\" {Ip} {Port} {KeyHash} \"{Password}\"";
}