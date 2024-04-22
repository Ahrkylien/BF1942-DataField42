using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;

public class Settings
{
    private BfServerManagerClientCommunication _communication;

    private Permissions? _permissions;
    public List<ISetting> List { get; set; } =
    [
        new StringSetting("Server name", "game.serverName", "BFServer", 1, 32),
        new BoolSetting("Dedicated", "game.serverDedicated", true),
        new IpSetting("IP", "game.serverIP", new IPAddress(0)),
        new BoolSetting("Bind all interfaces", "manager.bindAllIntefaces", false),
        new NumberSetting("Game port", "game.serverPort", 14567, 1024, 65535),
        new NumberSetting("Console port", "manager.consolePort", 4711, 1024, 65535),
        new NumberSetting("GameSpy port (internet)", "game.gameSpyPort", 23000, 1024, 65535),
        new NumberSetting("GameSpy port (LAN)", "game.gameSpyLANPort", 22000, 1024, 65535),
        new NumberSetting("ASE port", "game.ASEPort", 14690, 1024, 65535),
        new NumberSetting("Maximum players", "game.serverMaxPlayers", 32, 2, 256),
        new WelcomeMessageSetting("Welcome Message", "game.setServerWelcomeMessage", ""),
        new BoolSetting("Is internet server", "game.serverInternet", false),
        new MapSetting("Server bandwidth limit", "game.serverBandwidthChokeLimit", "0", new Dictionary<string, string>() { { "No Limit", "0" }, { "64 Kbps", "8" }, { "128 Kbps", "16" }, { "256 Kbps", "32" }, { "512 Kbps", "64" }, { "1024 Kbps", "128" }, { "2048 Kbps", "256" }, { "8192 Kbps", "1024" } }), // BFSM_SETTING_TYPE_MAP
        new MapSetting("Maximum client connection type", "game.serverMaxAllowedConnectionType", "CTLanT1", new Dictionary<string, string>() { { "Modem 56 Kbps", "CTModem56Kbps" }, { "Cable 128 Kbps", "CTCable128Kbps" }, { "Cable 256 Kbps", "CTCable256Kbps" }, { "Lan T1", "CTLanT1" } }), // BFSM_SETTING_TYPE_MAP
        new PercentageSetting("AI CPU", "game.serverCoopCPU", 100, minimumValue: 1, maximumValue: 100),
        new NumberSetting("Content check mode", "game.serverContentCheck", 0, 0, 2),
        new StringSetting("Server Password", "game.serverPassword", "", 0, 31),
        new NumberSetting("Rounds per level", "game.serverNumberOfRounds", 3, 0, 10),
        new NumberSetting("Round length (minutes)", "game.serverGameTime", 0, 0, 120),
        new NumberSetting("Round score limit", "game.serverScoreLimit", 0, 0, 120),
        new NumberSetting("Ticket ratio", "game.serverTicketRatio", 100, 10, 1000),
        new PercentageSetting("Attacker ticket modifier", "game.objectiveAttackerTicketsMod", 100, 50, 200),
        new PercentageSetting("AI skill", "game.serverCoopAISkill", 50, 1, 100),
        new NumberSetting("Allied team ratio", "game.serverAlliedTeamRatio", 1, 0, 10),
        new NumberSetting("Axis team ratio", "game.serverAxisTeamRatio", 1, 0, 10),
        new NumberSetting("Spawn time (seconds)", "game.serverSpawnTime", 15, 1, 120),
        new NumberSetting("Spawn delay (seconds)", "game.serverSpawnDelay", 5, 1, 30),
        new NumberSetting("Round start delay (seconds)", "game.serverGameStartDelay", 20, 0, 120),
        new BoolSetting("Use modified gravity", "manager.gravity", false),
        new NumberSetting("Gravity", "physics.gravity", -4, -15, -1),
        new NumberSetting("Map restart delay", "admin.timeBeforeRestartMap", 10, 5, 30),
        new BoolSetting("Enable PunkBuster", "game.serverPunkBuster", false),
        new BoolSetting("Ban players in both BF and Punkbuster", "manager.banInBFandPB", false),
        new PercentageSetting("Friendly fire on soldiers", "game.serverSoldierFriendlyFire", 100, 0, 200),
        new PercentageSetting("Friendly fire splash damage on soldiers", "game.serverSoldierFriendlyFireOnSplash", 100, 0, 200),
        new PercentageSetting("Friendly fire on vehicles", "game.serverVehicleFriendlyFire", 100, 0, 200),
        new PercentageSetting("Friendly fire splashdamage on vehicles", "game.serverVehicleFriendlyFireOnSplash", 100, 0, 200),
        new PercentageSetting("Friendly fire kickback", "game.serverKickback", 100, minimumValue: 0, maximumValue: 200, multiplierForToString: 0.01m),
        new PercentageSetting("Friendly fire splash damage kickback", "game.serverKickbackOnSplash", 100, minimumValue: 0, maximumValue: 200, multiplierForToString: 0.01m),
        new BoolSetting("Enable team kill forgive and punish", "game.serverTKPunishMode", false),
        new BoolSetting("Ban players when kicked for team kill", "admin.banPlayerOnTKKick", false),
        new NumberSetting("Number of team kills for automatic kick", "admin.nrOfTKToKick", 3, 0, 20),
        new NumberSetting("Spawn delay penalty for team kill (seconds)", "admin.spawnDelayPenaltyForTK", 0, 0, 100, multiplierForToString: 0.1m),
        new BoolSetting("Enable automatic kick for negative score", "manager.autoKickScore", false),
        new NumberSetting("Automatic kick for negative score value", "manager.autoKickScoreValue", -6, -25, -1),
        new BoolSetting("Enable automatic ban", "manager.autoBan", false),
        new NumberSetting("Enable automatic ban value", "manager.autoBanValue", 3, 1, 10),
        new BoolSetting("Enable kick player votes", "admin.enableKickPlayerVote", true),
        new BoolSetting("Enable map votes", "admin.enableMapVote", true),
        new BoolSetting("Enable kick team player votes", "admin.enableKickTeamPlayerVote", true),
        new BoolSetting("Auto balance teams", "game.serverAutoBalanceTeams", false),
        new BoolSetting("Use smart team balance", "manager.smartBalance", false),
        new NumberSetting("Smart team balance value", "manager.smartBalanceValue", 3, 2, 10),
        new NumberSetting("Vote duration (seconds)", "admin.votingTime", 60, 0, 120),
        new PercentageSetting("Map vote threshold percentage", "admin.voteMapMajority", 60, minimumValue: 1, multiplierForToString: 0.01m),
        new PercentageSetting("Kick player vote threshold percentage", "admin.voteKickPlayerMajority", 60, minimumValue: 1, multiplierForToString: 0.01m),
        new PercentageSetting("Kick team player vote threshold percentage", "admin.voteKickTeamPlayerMajority", 60, minimumValue: 1, multiplierForToString: 0.01m),
        new BoolSetting("Enable external camera views", "game.serverExternalViews", true),
        new BoolSetting("Enable external nose camera view in airplane", "game.serverAllowNoseCam", true),
        new BoolSetting("Enable free camera", "game.serverFreeCamera", false),
        new BoolSetting("Enable hit indication", "game.serverHitIndication", true),
        new BoolSetting("Enbale death camera to show killer", "game.serverDeathCameraType", false),
        new BoolSetting("Enable crosshair centerpoint", "game.serverCrosshairCenterpoint", true),
        new NumberSetting("Name tag distance (normal) [meters]", "game.serverNameTagDistance", 100, 0, 600),
        new NumberSetting("Name tag distance (aiming) [meters]", "game.serverNameTagDistanceScope", 300, 0, 600),
        new StringSetting("Remote console username", "manager.consoleUsername", "UserName", 0, 31),
        new StringSetting("Remote console password", "manager.consolePassword", "Password", 0, 31),
        new BoolSetting("Enable remote console", "manager.enableRemoteConsole", true),
        new BoolSetting("Enable remote admin", "manager.enableRemoteAdmin", false),
        new NumberSetting("Number of reserverd player slots", "game.serverNumReservedSlots", 0, 0, 256),
        new StringSetting("Reserverd slot password", "game.serverReservedPassword", "", 0, 31),
        new NumberSetting("Server monitor timer periond (seconds)", "manager.monitorTimerPeriod", 30, 10, 60),
        new BoolSetting("Enable auto-kick ping", "manager.autoKickPing", false),
        new NumberSetting("Auto kick ping theresthold value [ms]", "manager.autoKickPingValue", 500, 100, 500),
        new BoolSetting("Enable high ping warnings", "manager.highPingWarnings", false),
        new BoolSetting("admin.toggleGamePause??", "admin.toggleGamePause", false, alwaysIncludeInSerialization: true), // BFSM_SETTING_TYPE_PRESENT
        new BoolSetting("Enable auto kick on banned word", "manager.autoKickWord", false),
        new NumberSetting("Auto kick on banned word thresthold ", "manager.autoKickWordWarnings", 3, 0, 5),
        new BoolSetting("Enable announcements", "manager.autoAnnounce", false),
        new NumberSetting("Announcements period (seconds)", "manager.autoAnnouncePeriod", 60, 10, 1800),
        new BoolSetting("Enable server event logging", "game.serverEventLogging", false),
        new BoolSetting("Enable server event log compression", "game.serverEventLogCompression", false),
        new BoolSetting("Enable statistics gathering (csv)", "manager.statCollection", false),
        new StringSetting("Statistics filename", "manager.statFilePath", "statistics.csv", 0, 324),
    ];

    public event EventHandler? ServerPushedNewChanges;

    public Settings(BfServerManagerClientCommunication communication)
    {
        _communication = communication;
    }

    public void Initialize(Permissions permissions)
    {
        _permissions = permissions;
    }

    public void ParseSettingsFile(byte[] fileContents)
    {
        using MemoryStream memoryStream = new(fileContents);
        using StreamReader reader = new(memoryStream);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            string[] commandAndValue = line.Split(' ', 2);
            if (commandAndValue.Length == 2)
            {
                var command = commandAndValue[0];
                var value = commandAndValue[1].Trim('"');
                foreach (var setting in List)
                {
                    if (setting.BfServerManagerCommand == command)
                    {
                        setting.ParseValueFromServer(value);
                        break;
                    }
                }
            }
        }
    }

    public async Task Save(CancellationToken cancellationToken)
    {
        var settingsToSend = "";
        foreach (var setting in List)
        {
            if (setting.State == SettingState.Changed || setting.AlwaysIncludeInSerialization)
            {
                settingsToSend += setting.ToString() + "\n";
                setting.State = SettingState.Saved;
            }
        }
        await _communication.SendFile(FileAndCommands.ServerManager, Encoding.UTF8.GetBytes(settingsToSend), cancellationToken);
    }
}

public interface ISetting
{
    public string Name { get; set; }

    public string BfServerManagerCommand { get; set; }

    public SettingState State { get; set; }

    public bool AlwaysIncludeInSerialization { get; set; }

    public void ParseValueFromServer(string value);

    public string ToString();
}

public abstract class Setting<T> : ISetting
{
    public string Name { get; set; }

    public string BfServerManagerCommand { get; set; }

    protected T _value;
    public T Value
    {
        get { return _value; }
        set
        {
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                State = SettingState.Changed;
                _value = value;
            }
        }
    }

    public T OldValue { get; set; }

    public SettingState State { get; set; } = SettingState.Unchanged;

    public bool AlwaysIncludeInSerialization { get; set; } = false;

    public Setting(string name, string bfServerManagerCommand, T value)
    {
        Name = name;
        BfServerManagerCommand = bfServerManagerCommand;
        _value = value;
        OldValue = value;
    }

    public abstract void ParseValueFromServer(string value);

    public abstract override string ToString();
}

public class NumberSetting : Setting<decimal>
{
    public decimal MinimumValue { get; set; }
    public decimal MaximumValue { get; set; }
    public uint MaxDecimals { get; set; }
    public decimal MultiplierForToString { get; set; }
    public NumberSetting(string name, string bfServerManagerCommand, decimal value, decimal minimumValue, decimal maximumValue, uint maxDecimals = 0, decimal multiplierForToString = 1.0m) : base(name, bfServerManagerCommand, value)
    {
        MinimumValue = minimumValue;
        MaximumValue = maximumValue;
        MaxDecimals = maxDecimals;
        MultiplierForToString = multiplierForToString;
    }

    public void Validate(decimal value)
    {
        if (value < MinimumValue || value > MaximumValue)
            throw new ArgumentException($"{value} is out of range ({MinimumValue} -> {MaximumValue}).");
    }

    public override void ParseValueFromServer(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
            throw new ArgumentException($"{value} is not a valid number.");
        parsedValue /= MultiplierForToString;
        
        Validate(parsedValue);

        Value = parsedValue;
        OldValue = parsedValue;

        State = SettingState.Unchanged;
    }

    public override string ToString()
    {
        var numberOfDecimals = (int)MaxDecimals + (int)Math.Abs(Math.Log10((double)MultiplierForToString));
        var numberString = (Value * MultiplierForToString).ToString("0." + new string('0', numberOfDecimals), CultureInfo.InvariantCulture);
        return $"{BfServerManagerCommand} {numberString}";
    }
}

public class PercentageSetting : NumberSetting
{
    public PercentageSetting(string name, string bfServerManagerCommand, decimal value, decimal minimumValue = 0, decimal maximumValue = 100, uint maxDecimals = 0, decimal multiplierForToString = 1.0m) : base(name, bfServerManagerCommand, value, minimumValue, maximumValue, maxDecimals, multiplierForToString)
    {
    }
}

public class BoolSetting : Setting<bool>
{
    public BoolSetting(string name, string bfServerManagerCommand, bool value, bool alwaysIncludeInSerialization = false) : base(name, bfServerManagerCommand, value)
    {
        AlwaysIncludeInSerialization = alwaysIncludeInSerialization;
    }

    public override void ParseValueFromServer(string value)
    {
        if (!int.TryParse(value, out var parsedValue))
            throw new ArgumentException($"{value} is not a valid bool.");

        if (parsedValue != 0 && parsedValue != 1)
            throw new ArgumentException($"{value} is not a valid number for a bool (0 or 1).");

        Value = parsedValue == 1;
        OldValue = parsedValue == 1;

        State = SettingState.Unchanged;
    }

    public override string ToString() => $"{BfServerManagerCommand} {(Value ? 1 : 0)}";
}

public class StringSetting : Setting<string>
{
    public int MinimumLength { get; set; }
    public int MaximumLength { get; set; }
    public StringSetting(string name, string bfServerManagerCommand, string value, int minimumLength, int maximumLength) : base(name, bfServerManagerCommand, value)
    {
        MinimumLength = minimumLength;
        MaximumLength = maximumLength;
    }

    public override void ParseValueFromServer(string value)
    {
        if (value.Length < MinimumLength || value.Length > MaximumLength)
            throw new ArgumentException($"The string \"{value}\" is out of range ({MinimumLength} -> {MaximumLength}).");

        Value = value;
        OldValue = value;

        State = SettingState.Unchanged;
    }

    public override string ToString() => $"{BfServerManagerCommand} \"{Value}\"";
}

public class MapSetting : Setting<string>
{
    public Dictionary<string, string> Mapping { get; set; }
    public MapSetting(string name, string bfServerManagerCommand, string value, Dictionary<string, string> mapping) : base(name, bfServerManagerCommand, value)
    {
        Mapping = mapping;
    }

    public override void ParseValueFromServer(string value)
    {
        if (!Mapping.ContainsValue(value))
            throw new ArgumentException($"The string \"{value}\" is not a valid value for the mapped setting.");

        Value = Mapping.FirstOrDefault(x => x.Value == value).Key;
        OldValue = Value;

        State = SettingState.Unchanged;
    }

    public override string ToString() => $"{BfServerManagerCommand} {Mapping[Value]}";
}

public class WelcomeMessageSetting : Setting<string>
{
    public WelcomeMessageSetting(string name, string bfServerManagerCommand, string value) : base(name, bfServerManagerCommand, value)
    {
        AlwaysIncludeInSerialization = true;
    }

    public override void ParseValueFromServer(string value)
    {
        var values = value.Split(' ');
        var lineNumber = int.Parse(values[0]);
        value = values[1].Trim('"').Replace('_', ' ');

        if (lineNumber == 0)
            Value = value;
        else
            Value += "\n" + value;
        
        OldValue = Value;

        State = SettingState.Unchanged;
    }

    public override string ToString()
    {
        var outputString = "";
        var i = 0;
        foreach (var line in Value.Split('\n'))
        {
            outputString += $"{(i > 0 ? "\n" : "")}{BfServerManagerCommand} {i} \"{line.Replace(' ', '_')}\"";
            i++;
        }
        return outputString;
    }
}

public class IpSetting : Setting<IPAddress>
{
    public IpSetting(string name, string bfServerManagerCommand, IPAddress value) : base(name, bfServerManagerCommand, value)
    {
    }

    public override void ParseValueFromServer(string value)
    {
        if (!IPAddress.TryParse(value, out var parsedValue))
            throw new ArgumentException($"{value} is not a valid IP.");

        Value = parsedValue;
        OldValue = parsedValue;

        State = SettingState.Unchanged;
    }

    public override string ToString() => $"{BfServerManagerCommand} {Value}";
}

public enum SettingState
{
    Unchanged,
    Changed,
    Saved,
}