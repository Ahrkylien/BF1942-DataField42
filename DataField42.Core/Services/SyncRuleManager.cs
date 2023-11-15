using System.IO;

public class SyncRuleManager : ISyncRuleManager
{
    private readonly string _ruleFilePath;
    private readonly List<FileRule> _fileRules = new();

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
                    _fileRules.Add(fileRule);
                }
                catch (Exception ex)
                {
#if DEBUG
                    throw new Exception($"Can't parse line: {line} in: {_ruleFilePath}, Exception: {ex}");
#endif
                }
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
        foreach(var fileRule in _fileRules)
            if (fileRule.Matches(fileInfo))
                return fileRule.IgnoreSyncScenario;
        
        return IgnoreSyncScenarios.Never;
    }
}
