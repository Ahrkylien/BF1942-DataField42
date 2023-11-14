public interface ISyncRuleManager
{
    bool CheckIfFileShouldBeSynced(FileInfo fileInfo);
}

public class SyncRuleManagerDummy : ISyncRuleManager
{
    public bool CheckIfFileShouldBeSynced(FileInfo fileInfo)
    {
        return true;
    }
}
