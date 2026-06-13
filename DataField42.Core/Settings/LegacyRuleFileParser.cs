namespace DataField42.Settings;

public static class LegacyRuleFileParser
{
    public static (List<FileRule> IgnoreSyncRules, List<string> AutoSyncServers, bool AutoJoin) Parse(string filePath)
    {
        var ignoreSyncRules = new List<FileRule>();
        var autoSyncServers = new List<string>();
        var autoJoin = false;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (IOException)
        {
            return (ignoreSyncRules, autoSyncServers, autoJoin);
        }

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("//"))
                continue;

            var parts = line.Split(' ');

            if (parts[0] == "ignore" && parts.Length == 5)
            {
                try
                {
                    ignoreSyncRules.Add(new FileRule(parts[1], parts[2], parts[3], parts[4]));
                }
                catch
                {
#if DEBUG
                    throw new Exception($"Can't parse line: {line} in: {filePath}");
#endif
                }
            }
            else if (parts[0] == "autoSync" && parts.Length == 2)
            {
                autoSyncServers.Add(parts[1].ToLower());
            }
            else if (parts[0] == "autoJoin" && parts.Length == 1)
            {
                autoJoin = true;
            }
        }

        return (ignoreSyncRules, autoSyncServers, autoJoin);
    }
}
