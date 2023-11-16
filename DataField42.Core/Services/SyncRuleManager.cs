using System.IO;

public class SyncRuleManager : ISyncRuleManager
{
    private readonly string _ruleFilePath;
    private readonly List<FileRule> _ignoreFileSyncRules = new();
    private readonly List<string> _autoSyncEnabledServers = new();
    private bool _autoJoinEnabled = false;

    public SyncRuleManager(string ruleFilePath)
    {
        _ruleFilePath = ruleFilePath;
        _parseRuleFile();
    }

    /// <summary>
    /// Parses the rule file while swallowing parsing errors
    /// </summary>
    private void _parseRuleFile() {
        string[] lines = File.ReadAllLines(_ruleFilePath);
        foreach(var line in lines)
        {
            if (line.TrimStart().StartsWith("//")) // comment
                continue;
            
            var lineParts = line.Split(' ');
            /*
            ignore sync_mode type mod file => ignore always|different_version|when_dependency movie|file|archive|level bf1942 berlin
            dont_ignore type mod file
            */
            if (lineParts[0] == "ignore" && lineParts.Length == 5)
            {
                try
                {
                    var fileRule = new FileRule(lineParts[1], lineParts[2], lineParts[3], lineParts[4]);
                    _ignoreFileSyncRules.Add(fileRule);
                }
                catch (Exception ex)
                {
#if DEBUG
                    throw new Exception($"Can't parse line: {line} in: {_ruleFilePath}, Exception: {ex}");
#endif
                }
            }
            else if (lineParts[0] == "autoSync" && lineParts.Length == 2)
            {
                _autoSyncEnabledServers.Add(lineParts[1].ToLower());
            }
            else if (lineParts[0] == "autoJoin" && lineParts.Length == 1)
            {
                _autoJoinEnabled = true;
            }
        }
    }

    /// <summary>
    /// First rule matching the FileInfo will be applied
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns></returns>
    public IgnoreSyncScenarios GetIgnoreFileSyncScenario(FileInfo fileInfo)
    {
        foreach(var fileRule in _ignoreFileSyncRules)
            if (fileRule.Matches(fileInfo))
                return fileRule.IgnoreSyncScenario;
        
        return IgnoreSyncScenarios.Never;
    }


    public bool IsAutoSyncEnabled(string DomainOrIp) => _autoSyncEnabledServers.Contains(DomainOrIp.ToLower());

    public void AutoSyncEnable(string DomainOrIp)
    {
        if (!IsAutoSyncEnabled(DomainOrIp))
        {
            _autoSyncEnabledServers.Add(DomainOrIp);
            File.AppendAllText(_ruleFilePath, $"\nautoSync {DomainOrIp}");
        }
    }

    public bool IsAutoJoinEnabled() => _autoJoinEnabled;

    public void AutoJoinEnable()
    {
        if (!IsAutoJoinEnabled())
        {
            _autoJoinEnabled = true;
            File.AppendAllText(_ruleFilePath, $"\nautoJoin");
        }
        
    }
}
