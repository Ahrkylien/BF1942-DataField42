public interface ISyncRuleManager
{
    IgnoreSyncScenarios GetIgnoreFileSyncScenario(FileInfo fileInfo);
}

public class SyncRuleManagerDummy : ISyncRuleManager
{
    public IgnoreSyncScenarios GetIgnoreFileSyncScenario(FileInfo fileInfo) => IgnoreSyncScenarios.Never;
}
