public interface ISyncRuleManager
{
    IgnoreSyncScenarios GetIgnoreFileSyncScenario(FileInfo fileInfo);
    bool IsAutoSyncEnabled(string DomainOrIp);
    void AutoSyncEnable(string DomainOrIp);
}
