namespace DataField42;

public class SyncParameters
{
    public string Mod;
    public string Map;
    public string Ip;
    public int Port;
    public string KeyHash;

    public SyncParameters(string mod, string map, string hostName, int port, string keyHash)
    {
        Mod = mod;
        Map = map;
        Ip = hostName;
        Port = port;
        KeyHash = keyHash;
    }
}