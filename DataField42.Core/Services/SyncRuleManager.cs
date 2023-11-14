using System.IO;

public class SyncRuleManager : ISyncRuleManager
{
    private string _ruleFilePath;
    private List<FileRule> _fileRules;

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
                var syncScenario = Enum.Parse<SyncScenarios>(lineParts[1], true);
                var fileType = Enum.Parse<Bf1942FileTypes>(lineParts[2], true);
                var mod = lineParts[3];
                var fileName = lineParts[4];

                continue;
            }
        }
    }

    public bool CheckIfFileShouldBeSynced(FileInfo fileInfo)
    {
        return false;
    }
}
