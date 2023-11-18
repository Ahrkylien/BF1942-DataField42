using Microsoft.Win32;

public static class CommandLineArguments
{
    public static CommandLineArgumentIdentifier Identifier { get; set; }
    public static string Mod { get; set; } = "BF1942";
    /// <summary>
    /// Map name with underscores
    /// </summary>
    public static string Map { get; set; }
    public static string KeyRegisterPath { get; set; }
    public static string Key { get; set; }
    public static string KeyHash { get; set; }
    public static string Ip { get; set; }
    public static int Port { get; set; }
    public static string Password { get; set; }

    public static void Parse(string[] arguments)
    {
        // DownloadAndJoinServer
        if(arguments.Length >= 2)
        {
            if (arguments[1] == "map" || arguments[1] == "mod")
                Identifier = CommandLineArgumentIdentifier.DownloadAndJoinServer;
            else
                Identifier = CommandLineArgumentIdentifier.Unknown;
        }
        else
        {
            Identifier = CommandLineArgumentIdentifier.None;
        }

        if (arguments.Length == 7 && arguments[1] == "map")
            _parseMapMod(arguments[2], arguments[3], arguments[4], arguments[5], arguments[6]);
        else if (arguments.Length == 6 && arguments[1] == "mod") // TODO: patch asm to have * for map path for mod
            _parseMapMod(arguments[2], arguments[3], arguments[4], "*", arguments[5]);
    }

    private static void _parseMapMod(string keyRegisterPath, string ipPort, string password, string mapath, string mod)
    {
        if (mapath != "*")
            if (!(mapath.ToLower().StartsWith("bf1942/levels/") && mapath.EndsWith("/")))
                throw new ArgumentException($"Server has send an illegal map path: {mapath}");

        var map = mapath[14..^1];
        if(!(map.All(c => char.IsLetterOrDigit(c) || c.Equals('_')) && mapath.Length >= 1)) // only letters digits and underscores and at least 1 char
            throw new ArgumentException($"Server has send an illegal map name: {map}");

        if (!(mod.Length >= 1))
            throw new ArgumentException($"Server has send an illegal mod name: {mod}");


        KeyRegisterPath = keyRegisterPath;
        Key = _readRegistryKey(KeyRegisterPath);
        KeyHash = _createMd5String(Key);
        Ip = ipPort.Split(':')[0];
        Port = int.Parse(ipPort.Split(':')[1]);
        Password = password;
        Map = map;
        Mod = mod;
    }

    private static string _readRegistryKey(string subKey)
    {
#pragma warning disable CA1416 // Validate platform compatibility
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subKey, false);
        string keyValue = key?.GetValue(null)?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(keyValue))
            throw new Exception($"Cant find key at: {subKey}, value: {keyValue}");
        return keyValue;
#pragma warning restore CA1416 // Validate platform compatibility
    }

    private static string _createMd5String(string inputString)
    {
        var inputBytes = System.Text.Encoding.ASCII.GetBytes(inputString);
        var hashBytes = System.Security.Cryptography.MD5.Create().ComputeHash(inputBytes);
        var hashString = Convert.ToHexString(hashBytes);
        if (hashString.Length != 32)
            throw new Exception($"MD5 calcuation of {inputString} gave incorrect length: {hashBytes}");
        return hashString;
    }
}