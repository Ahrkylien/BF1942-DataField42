using System.Net;

public class Bf1942QueryResult
{
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
    public string GameType { get; init; }
    public string GameMode { get; init; }

    public int Tickets1 { get; init; }
    public int Tickets2 { get; init; }

    /// <summary>
    /// In seconds
    /// </summary>
    public int RoundTime { get; init; }

    /// <summary>
    /// In seconds
    /// </summary>
    public int RoundTimeRemain { get; init; }

    public bool AutoBalanceTeams { get; init; }
    public bool UsesPunkbuster { get; init; }
    public bool HitIndicator { get; init; }
    public bool FreeCamera { get; init; }
    public bool ExternalView { get; init; }
    public bool AllowNoseCam { get; init; }
    public DedicatedServerType DedicatedServerType { get; init; }

    public int ReservedSlots { get; init; }
    public int NumberOfRounds { get; init; }
    public int NameTagDistance { get; init; }
    public int NameTagDistanceScope { get; init; }

    /// <summary>
    /// Total round time in minutes.
    /// Null means unlimited.
    /// </summary>
    public int? TimeLimit { get; init; }
    public int AlliedTeamRatio { get; init; }
    public int AxisTeamRatio { get; init; }
    public int BandwidthChokeLimit { get; init; }
    public int ContentCheck { get; init; }
    public int AverageFps { get; init; }
    public int Cpu { get; init; }
    public int Status { get; init; }

    public int TicketRatio { get; init; }
    public int SpawnDelay { get; init; }
    public int SpawnWaveTime { get; init; }
    public string TkMode { get; init; }
    public int KickBack { get; init; }
    public int KickBackOnSplash { get; init; }
    public int SoldierFriendlyFire { get; init; }
    public int SoldierFriendlyFireOnSplash { get; init; }
    public int VehicleFriendlyFire { get; init; }
    public int VehicleFriendlyFireOnSplash { get; init; }
    public int GameStartDelay { get; init; }
    public string ActiveMods { get; init; }
    public List<string> UnpureMods { get; init; }

    public List<Player> Players { get; init; }

    /// <summary>
    /// Windows server only
    /// </summary>
    public int? Location { get; init; }

    /// <summary>
    /// Windows server only
    /// </summary>
    public string? Language { get; init; }

    public Bf1942QueryResult(Dictionary<string, string> properties)
    {
        GameName = Str(properties, "gamename");
        GameVersion = Str(properties, "gamever");
        GameId = Str(properties, "gameId");
        HostPort = Uint(properties, "hostport");
        HostName = Str(properties, "hostname");
        MapName = Str(properties, "mapname");
        NumberOfPlayers = Uint(properties, "numplayers");
        MaximumNumberOfPlayers = Uint(properties, "maxplayers");
        HasPassword = Bool(properties, "password");
        Mod = Str(properties, "mapId");
        GameType = Str(properties, "gametype");
        GameMode = Str(properties, "gamemode");

        Tickets1 = Int(properties, "tickets1");
        Tickets2 = Int(properties, "tickets2");

        RoundTime = Int(properties, "roundTime");
        RoundTimeRemain = Int(properties, "roundTimeRemain");

        AutoBalanceTeams = Bool(properties, "auto_balance_teams");
        UsesPunkbuster = Bool(properties, "sv_punkbuster");
        HitIndicator = Bool(properties, "hit_indicator");
        FreeCamera = Bool(properties, "free_camera");
        ExternalView = Bool(properties, "external_view");
        AllowNoseCam = Bool(properties, "allow_nose_cam");

        DedicatedServerType = (DedicatedServerType)Int(properties, "dedicated");
        ReservedSlots = Int(properties, "reservedslots");
        NumberOfRounds = Int(properties, "number_of_rounds");
        NameTagDistance = Int(properties, "name_tag_distance");
        NameTagDistanceScope = Int(properties, "name_tag_distance_scope");
        TimeLimit = IntThatCanBeInfinite(properties, "time_limit");
        AlliedTeamRatio = Int(properties, "allied_team_ratio");
        AxisTeamRatio = Int(properties, "axis_team_ratio");
        BandwidthChokeLimit = Int(properties, "bandwidth_choke_limit");
        ContentCheck = Int(properties, "content_check");
        AverageFps = Int(properties, "averageFPS");
        Cpu = Int(properties, "cpu");
        Status = Int(properties, "status");

        TicketRatio = IntWithPostfix(properties, "ticket_ratio", '%');
        SpawnDelay = IntWithPostfix(properties, "spawn_delay", 's');
        SpawnWaveTime = IntWithPostfix(properties, "spawn_wave_time", 's');
        TkMode = Str(properties, "tk_mode");
        KickBack = IntWithPostfix(properties, "kickback", '%');
        KickBackOnSplash = IntWithPostfix(properties, "kickback_on_splash", '%');
        SoldierFriendlyFire = IntWithPostfix(properties, "soldier_friendly_fire", '%');
        SoldierFriendlyFireOnSplash = IntWithPostfix(properties, "soldier_friendly_fire_on_splash", '%');
        VehicleFriendlyFire = IntWithPostfix(properties, "vehicle_friendly_fire", '%');
        VehicleFriendlyFireOnSplash = IntWithPostfix(properties, "vehicle_friendly_fire_on_splash", '%');
        GameStartDelay = IntWithPostfix(properties, "game_start_delay", 's');
        ActiveMods = Str(properties, "active_mods");

        var unpureStr = Str(properties, "unpure_mods");
        UnpureMods = string.IsNullOrEmpty(unpureStr)
            ? []
            : [.. unpureStr.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)];

        Players = [];
        for (int i = 0; i < NumberOfPlayers; i++)
        {
            Players.Add(new Player(
                Bf1942Encoding.Decode(Bf1942Encoding.Encode(Str(properties, "playername_" + i)), applySmartEncodingDetection: true),
                Str(properties, "team_" + i),
                Str(properties, "score_" + i),
                Str(properties, "kills_" + i),
                Str(properties, "deaths_" + i),
                Str(properties, "ping_" + i),
                properties.ContainsKey("keyhash_" + i) ? Str(properties, "keyhash_" + i) : ""
            ));
        }

        Location = properties.ContainsKey("location") ? Int(properties, "location") : null;
        Language = properties.ContainsKey("language") ? Str(properties, "language") : null;
    }

    private static string Str(Dictionary<string, string> p, string key) => p[key];

    private static int IntWithPostfix(Dictionary<string, string> p, string key, char postfix)
    {
        try
        {
            var value = p[key];
            if (!value.EndsWith(postfix))
                throw new FormatException();
            return int.Parse(value[..^1]);
        }
        catch (FormatException)
        {
            throw new ProtocolViolationException($"Unexpected int (with postfix {postfix}) value for {key}: {p[key]}");
        }
    }

    private static int Int(Dictionary<string, string> p, string key)
    {
        try
        {
            return int.Parse(p[key]);
        }
        catch (FormatException)
        {
            throw new ProtocolViolationException($"Unexpected int value for {key}: {p[key]}");
        }
    }

    private static int? IntThatCanBeInfinite(Dictionary<string, string> p, string key)
    {
        if (p[key] == "unlimited")
            return null;

        try
        {
            return int.Parse(p[key]);
        }
        catch (FormatException)
        {
            throw new ProtocolViolationException($"Unexpected int value for {key}: {p[key]}");
        }
    }

    private static uint Uint(Dictionary<string, string> p, string key)
    {
        try
        {
            return uint.Parse(p[key]);
        }
        catch (FormatException)
        {
            throw new ProtocolViolationException($"Unexpected uint value for {key}: {p[key]}");
        }
    }

    private static bool Bool(Dictionary<string, string> p, string key)
    {
        if (p[key] is "on" or "yes" or "1")
            return true;
        else if (p[key] is "off" or "no" or "0")
            return false;
        throw new ProtocolViolationException($"Unexpected bool value for {key}: {p[key]}");
    }
}
