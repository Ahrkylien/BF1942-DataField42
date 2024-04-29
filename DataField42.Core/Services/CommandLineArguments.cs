using System.Text.RegularExpressions;

public static class CommandLineArguments
{
    public static CommandLineArgumentIdentifier Identifier { get; set; }
    public static string Mod { get; set; } = "BF1942";
    /// <summary>
    /// Map name with underscores
    /// </summary>
    public static string Map { get; set; }
    public static string KeyHash { get; set; }
    public static string Ip { get; set; }
    public static int Port { get; set; }
    public static string Password { get; set; }

    public static string? RawString => Environment.GetCommandLineArgs().Length > 1 ? "\"" + string.Join("\" \"", Environment.GetCommandLineArgs()[1..]) + "\"" : "";

    public static void Parse(string[] arguments)
    {
        // DownloadAndJoinServer
        if(arguments.Length >= 2)
        {
            if (arguments[1] == "map" || arguments[1] == "mod" || arguments[1] == "SyncAndJoinServer")
                Identifier = CommandLineArgumentIdentifier.SyncAndJoinServer;
            else if (arguments[1] == "install")
                Identifier = CommandLineArgumentIdentifier.Install;
            else
                Identifier = CommandLineArgumentIdentifier.Unknown;
        }
        else
        {
            Identifier = CommandLineArgumentIdentifier.None;
        }

        if (arguments.Length == 7 && arguments[1] == "map")
            ParseArgumentsFromBf1942(arguments[2], arguments[3], arguments[4], arguments[5], arguments[6]);
        else if (arguments.Length == 6 && arguments[1] == "mod") // TODO: patch asm to have * for map path for mod
            ParseArgumentsFromBf1942(arguments[2], arguments[3], arguments[4], "*", arguments[5]);
        else if (arguments.Length == 8 && arguments[1] == "SyncAndJoinServer")
            ParseArguments(arguments[6], $"{arguments[4]}:{arguments[5]}", arguments[7], arguments[3], arguments[2]);
    }

    private static void ParseArgumentsFromBf1942(string keyRegisterPath, string ipPort, string password, string mapath, string mod)
    {
        var key = Registry.ReadKey(keyRegisterPath);

        if (mapath != "*")
            if (!(mapath.ToLower().StartsWith("bf1942/levels/") && mapath.EndsWith("/")))
                throw new ArgumentException($"Server has send an illegal map path: {mapath}");

        var map = mapath == "*" ? mapath : mapath[14..^1];

        ParseArguments(Md5.Hash(key), ipPort, password, map, mod);
    }

    private static void ParseArguments(string keyHash, string ipPort, string password, string map, string mod)
    {
        if (map != "*")
            if (!(Regex.IsMatch(map, $"^[{FileInfo.AllowableChars}]*$") && map.Length >= 1)) // only letters digits and underscores and hyphens and at least 1 char
                throw new ArgumentException($"Server has send an illegal map name: {map}");

        if (!(Regex.IsMatch(mod, $"^[{FileInfo.AllowableChars}]*$") && mod.Length >= 1)) // only letters digits and underscores and hyphens and at least 1 char
            throw new ArgumentException($"Server has send an illegal mod name: {mod}");

        KeyHash = keyHash;
        Ip = ipPort.Split(':')[0];
        Port = int.Parse(ipPort.Split(':')[1]);
        Password = password;
        Map = map;
        Mod = mod;
    }
}