public interface ISyncRuleManager
{
    IgnoreSyncScenario GetIgnoreFileSyncScenario(FileInfo fileInfo);
    bool IsAutoSyncEnabled(string DomainOrIp);
    void AutoSyncEnable(string DomainOrIp);
    bool IsAutoJoinEnabled();
    void AutoJoinEnable();
}
