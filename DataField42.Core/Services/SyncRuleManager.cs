using DataField42.Settings;

public class SyncRuleManager : ISyncRuleManager
{
    private readonly SettingsService _settingsService;

    public SyncRuleManager(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// First rule matching the FileInfo will be applied
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns></returns>
    public IgnoreSyncScenarios GetIgnoreFileSyncScenario(FileInfo fileInfo)
    {
        foreach(var fileRule in _settingsService.Settings.IgnoreSyncRules)
            if (fileRule.Matches(fileInfo))
                return fileRule.IgnoreSyncScenario;
        
        return IgnoreSyncScenarios.Never;
    }

    public bool IsAutoSyncEnabled(string DomainOrIp) => _settingsService.Settings.AutoSyncServers.Contains(DomainOrIp.ToLower());

    public void AutoSyncEnable(string DomainOrIp)
    {
        if (!IsAutoSyncEnabled(DomainOrIp))
        {
            _settingsService.Settings.AutoSyncServers.Add(DomainOrIp);
            _settingsService.Save();
        }
    }

    public bool IsAutoJoinEnabled() => _settingsService.Settings.AutoJoin;

    public void AutoJoinEnable()
    {
        if (!IsAutoJoinEnabled())
        {
            _settingsService.Settings.AutoJoin = true;
            _settingsService.Save();
        }
    }
}
