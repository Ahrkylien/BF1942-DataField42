public class DownloadDecisionMaker
{
    private ISyncRuleManager _syncRuleManager;
    private ILocalFileCacheManager _localFileCacheManager;

    public DownloadDecisionMaker(ISyncRuleManager syncRuleManager, ILocalFileCacheManager localFileCacheManager)
    {
        _syncRuleManager = syncRuleManager;
        _localFileCacheManager = localFileCacheManager;
    }

    public void CheckDownloadRequests(List<FileInfo> fileInfos)
    {
        foreach (FileInfo fileInfo in fileInfos)
            CheckDownloadRequest(fileInfo);
    }

    public void CheckDownloadRequest(FileInfo fileInfo)
    {
        var ignoreSyncScenario = _syncRuleManager.GetIgnoreFileSyncScenario(fileInfo);
        if (ignoreSyncScenario == IgnoreSyncScenarios.Always)
            fileInfo.SyncType = SyncType.None;
        else if (ignoreSyncScenario == IgnoreSyncScenarios.DifferentVersion && _localFileCacheManager.CheckIfSimilarFileExistsInGame(fileInfo))
            fileInfo.SyncType = SyncType.None;
        else if (_localFileCacheManager.CheckIfFileExistsInGame(fileInfo))
            fileInfo.SyncType = SyncType.LocalFile;
        else if (_localFileCacheManager.CheckIfFileExistsInCache(fileInfo))
            fileInfo.SyncType = SyncType.LocalFileCache;
        else
            fileInfo.SyncType = SyncType.Download;
    }
}
