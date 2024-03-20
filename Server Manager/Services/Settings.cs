using System.Globalization;
using System.Net;

public class Settings
{
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
        new NumberSetting("Server bandwidth limit", "game.serverBandwidthChokeLimit", 0, 0, 32), // BFSM_SETTING_TYPE_MAP
        new StringSetting("Maximum client connection type", "game.serverMaxAllowedConnectionType", "CTLanT1", 0, 99), // BFSM_SETTING_TYPE_MAP
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
        new PercentageSetting("Game port", "admin.voteMapMajority", 60, minimumValue: 1, multiplierForToString: 0.01m),
        new PercentageSetting("Game port", "admin.voteKickPlayerMajority", 60, minimumValue: 1, multiplierForToString: 0.01m),
        new PercentageSetting("Game port", "admin.voteKickTeamPlayerMajority", 60, minimumValue: 1, multiplierForToString: 0.01m),
        new BoolSetting("Game port", "game.serverExternalViews", true),
        new BoolSetting("Game port", "game.serverAllowNoseCam", true),
        new BoolSetting("Game port", "game.serverFreeCamera", false),
        new BoolSetting("Game port", "game.serverHitIndication", true),
        new BoolSetting("Game port", "game.serverDeathCameraType", false),
        new BoolSetting("Game port", "game.serverCrosshairCenterpoint", true),
        new NumberSetting("Game port", "game.serverNameTagDistance", 100, 0, 600),
        new NumberSetting("Game port", "game.serverNameTagDistanceScope", 300, 0, 600),
        new StringSetting("Game port", "manager.consoleUsername", "UserName", 0, 31),
        new StringSetting("Game port", "manager.consolePassword", "Password", 0, 31),
        new BoolSetting("Game port", "manager.enableRemoteConsole", true),
        new BoolSetting("Game port", "manager.enableRemoteAdmin", false),
        new NumberSetting("Game port", "game.serverNumReservedSlots", 0, 0, 256),
        new StringSetting("Game port", "game.serverReservedPassword", "", 0, 31),
        new NumberSetting("Game port", "manager.monitorTimerPeriod", 30, 10, 60),
        new BoolSetting("Game port", "manager.autoKickPing", false),
        new NumberSetting("Game port", "manager.autoKickPingValue", 500, 100, 500),
        new BoolSetting("Game port", "manager.highPingWarnings", false),
        new BoolSetting("Game port", "admin.toggleGamePause", false), // BFSM_SETTING_TYPE_PRESENT
        new BoolSetting("Game port", "manager.autoKickWord", false),
        new NumberSetting("Game port", "manager.autoKickWordWarnings", 3, 0, 5),
        new BoolSetting("Game port", "manager.autoAnnounce", false),
        new NumberSetting("Game port", "manager.autoAnnouncePeriod", 60, 10, 1800),
        new BoolSetting("Game port", "game.serverEventLogging", false),
        new BoolSetting("Game port", "game.serverEventLogCompression", false),
        new BoolSetting("Game port", "manager.statCollection", false),
        new StringSetting("Game port", "manager.statFilePath", "statistics.csv", 0, 324),
    ];

    public event EventHandler? ServerPushedNewChanges;

    public Settings()
    {

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
}

public interface ISetting
{
    public string Name { get; set; }

    public string BfServerManagerCommand { get; set; }

    public SettingState State { get; set; }

    public void ParseValueFromServer(string value);

    public string ToString();
}

public abstract class Setting<T> : ISetting
{
    public string Name { get; set; }

    public string BfServerManagerCommand { get; set; }

    public T Value { get; set; }

    public T OldValue { get; set; }

    public SettingState State { get; set; }

    public Setting(string name, string bfServerManagerCommand, T value)
    {
        Name = name;
        BfServerManagerCommand = bfServerManagerCommand;
        Value = value;
        OldValue = value;
        State = SettingState.Unchanged;
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

    public void ChangeValue(decimal value)
    {
        if (value != Value)
        {
            Validate(value);
            Value = value;
            State = SettingState.Changed;
        }
    }

    public void Save()
    {
        if (State == SettingState.Changed)
            State = SettingState.Saved;
    }

    public void Validate(decimal value)
    {
        if (value < MinimumValue || value > MaximumValue)
            throw new ArgumentException($"{value} is out of range ({MinimumValue} -> {MaximumValue}).");
    }

    public override void ParseValueFromServer(string value)
    {
        if (!decimal.TryParse(value, out var parsedValue))
            throw new ArgumentException($"{value} is not a valid number.");

        Validate(parsedValue);

        Value = parsedValue;
        OldValue = parsedValue;
    }

    public override string ToString()
    {
        var numberOfDecimals = (int)MaxDecimals + (int)Math.Abs(Math.Log10((double)MultiplierForToString));
        var numberString = Value.ToString("0." + new string('0', numberOfDecimals), CultureInfo.InvariantCulture);
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
    public BoolSetting(string name, string bfServerManagerCommand, bool value) : base(name, bfServerManagerCommand, value)
    {
    }

    public override void ParseValueFromServer(string value)
    {
        if (!int.TryParse(value, out var parsedValue))
            throw new ArgumentException($"{value} is not a valid bool.");

        if (parsedValue != 0 && parsedValue != 1)
            throw new ArgumentException($"{value} is not a valid number for a bool (0 or 1).");

        Value = parsedValue == 1;
        OldValue = parsedValue == 1;
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
    }

    public override string ToString() => $"{BfServerManagerCommand} \"{Value}\"";
}

public class WelcomeMessageSetting : Setting<string>
{
    public WelcomeMessageSetting(string name, string bfServerManagerCommand, string value) : base(name, bfServerManagerCommand, value)
    {
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
    }

    public override string ToString() => $"{BfServerManagerCommand} {Value}";
}

public enum SettingState
{
    Unchanged,
    Changed,
    Saved,
}