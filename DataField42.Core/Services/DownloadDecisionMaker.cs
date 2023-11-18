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
        List<FileInfo> checkedFileInfos = new();
        foreach (FileInfo fileInfo in fileInfos)
        {
            var ignoreSyncScenario = _syncRuleManager.GetIgnoreFileSyncScenario(fileInfo);
            if (CheckIfAlreadyInList(checkedFileInfos, fileInfo)) // TODO: add some warning in UI
                fileInfo.SyncType = SyncType.None;
            else if(ignoreSyncScenario == IgnoreSyncScenarios.Always)
                fileInfo.SyncType = SyncType.None;
            else if (ignoreSyncScenario == IgnoreSyncScenarios.DifferentVersion && _localFileCacheManager.CheckIfSimilarFileExistsInGame(fileInfo))
                fileInfo.SyncType = SyncType.None;
            else if (_localFileCacheManager.CheckIfFileExistsInGame(fileInfo))
                fileInfo.SyncType = SyncType.LocalFile;
            else if (_localFileCacheManager.CheckIfFileExistsInCache(fileInfo))
                fileInfo.SyncType = SyncType.LocalFileCache;
            else
                fileInfo.SyncType = SyncType.Download;
            checkedFileInfos.Add(fileInfo);
        }
    }

    private bool CheckIfAlreadyInList(List<FileInfo> checkedFileInfos, FileInfo fileInfo)
    {
        foreach (FileInfo checkedFileInfo in checkedFileInfos)
            if (fileInfo.Mod.ToLower() == checkedFileInfo.Mod.ToLower() && fileInfo.FileType == checkedFileInfo.FileType && fileInfo.FileName.ToLower() == checkedFileInfo.FileName.ToLower())
                return true;
        return false;
    }
}
