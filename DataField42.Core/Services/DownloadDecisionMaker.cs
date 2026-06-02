using Microsoft.Extensions.Logging;

public class DownloadDecisionMaker
{
    private readonly ISyncRuleManager _syncRuleManager;
    private readonly ILocalFileCacheManager _localFileCacheManager;
    private readonly ILogger<DownloadDecisionMaker> _logger;

    public DownloadDecisionMaker(
        ISyncRuleManager syncRuleManager,
        ILocalFileCacheManager localFileCacheManager,
        ILogger<DownloadDecisionMaker> logger)
    {
        _syncRuleManager = syncRuleManager;
        _localFileCacheManager = localFileCacheManager;
        _logger = logger;
    }

    public async Task CheckDownloadRequests(List<FileInfo> fileInfos, CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Checking download requirements for {fileInfos.Count} files.");

        List<FileInfo> checkedFileInfos = new();
        foreach (FileInfo fileInfo in fileInfos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ignoreSyncScenario = _syncRuleManager.GetIgnoreFileSyncScenario(fileInfo);

            if (CheckIfAlreadyInList(checkedFileInfos, fileInfo)) // TODO: add some warning in UI
            {
                fileInfo.SyncType = SyncType.None;
                _logger.LogDebug($"File {fileInfo.FilePath} — skipped (duplicate in list).");
            }
            else if (ignoreSyncScenario == IgnoreSyncScenarios.Always)
            {
                fileInfo.SyncType = SyncType.None;
                _logger.LogDebug($"File {fileInfo.FilePath} — skipped (rule: Always ignore).");
            }
            else if (ignoreSyncScenario == IgnoreSyncScenarios.DifferentVersion && _localFileCacheManager.CheckIfSimilarFileExistsInGame(fileInfo))
            {
                fileInfo.SyncType = SyncType.None;
                _logger.LogDebug($"File {fileInfo.FilePath} — skipped (rule: DifferentVersion, similar file exists in game).");
            }
            else if (_localFileCacheManager.CheckIfFileExistsInGame(fileInfo))
            {
                fileInfo.SyncType = SyncType.LocalFile;
                _logger.LogDebug($"File {fileInfo.FilePath} — already in game directory.");
            }
            else if (_localFileCacheManager.CheckIfFileExistsInCache(fileInfo))
            {
                fileInfo.SyncType = SyncType.LocalFileCache;
                _logger.LogDebug($"File {fileInfo.FilePath} — found in local cache.");
            }
            else
            {
                fileInfo.SyncType = SyncType.Download;
                _logger.LogDebug($"File {fileInfo.FilePath} — queued for download.");
            }

            checkedFileInfos.Add(fileInfo);
            await Task.Yield();
        }

        _logger.LogDebug($"Download decision check complete for {fileInfos.Count} files.");
    }

    private static bool CheckIfAlreadyInList(List<FileInfo> checkedFileInfos, FileInfo fileInfo)
    {
        foreach (FileInfo checkedFileInfo in checkedFileInfos)
            if (fileInfo.Mod.Equals(checkedFileInfo.Mod, StringComparison.CurrentCultureIgnoreCase)
                && fileInfo.FileType == checkedFileInfo.FileType
                && fileInfo.FileName.Equals(checkedFileInfo.FileName, StringComparison.CurrentCultureIgnoreCase))
                return true;
        return false;
    }
}
