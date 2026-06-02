using Microsoft.Extensions.Logging;

public class DownloadManager
{
    private readonly DataField42Communication _communication;
    private readonly DownloadDecisionMaker _downloadDecisionMaker;
    private readonly ILocalFileCacheManager _localFileCacheManager;
    private readonly ILogger<DownloadManager> _logger;

    private string? _mod;
    private string? _map;
    private List<FileInfo>? _fileInfos;

    public DownloadManager(
        DataField42Communication dataField42Communication,
        DownloadDecisionMaker downloadDecisionMaker,
        ILocalFileCacheManager localFileCacheManager,
        ILogger<DownloadManager> logger)
    {
        _communication = dataField42Communication;
        _downloadDecisionMaker = downloadDecisionMaker;
        _localFileCacheManager = localFileCacheManager;
        _logger = logger;
    }

    /// <summary>Step 1 in synchronizing files.</summary>
    public async Task<IEnumerable<FileInfo>> DownloadFilesRequest(string mod, string map, string ip, int port, string keyHash, CancellationToken cancellationToken)
    {
        _mod = mod;
        _map = map;

        _logger.LogInformation($"Requesting file list for mod={mod}, map={map} from {ip}:{port}.");

        _localFileCacheManager.RemoveWorkingDirectory();
        _communication.StartSession();
        _communication.SendString($"download {map} {mod} {ip} {port} {keyHash}");

        _fileInfos = await _communication.ReceiveFileInfos(cancellationToken);
        _logger.LogDebug($"Received {_fileInfos.Count} file infos from server.");

        // TODO: better messaging for double files in list 
        // TODO: check for absense of base rfa

        if (_fileInfos.Count > 100) // TODO: check a resonable max
            throw new Exception($"Server wants to sync {_fileInfos.Count} files which is more than 100");

        await _downloadDecisionMaker.CheckDownloadRequests(_fileInfos, cancellationToken);

        var toDownload = _fileInfos.Count(x => x.SyncType == SyncType.Download);
        var fromCache = _fileInfos.Count(x => x.SyncType == SyncType.LocalFileCache);
        var local = _fileInfos.Count(x => x.SyncType == SyncType.LocalFile);
        var skipped = _fileInfos.Count(x => x.SyncType == SyncType.None);
        _logger.LogInformation($"Download decision: {toDownload} to download, {fromCache} from cache, {local} already local, {skipped} skipped.");

        return _fileInfos;
    }

    /// <summary>Step 2 in synchronizing files.</summary>
    /// <returns>(hasMod, hasMap)</returns>
    public (bool, bool) VerifyFileList()
    {
        if (_fileInfos == null)
            throw new ArgumentNullException($"{nameof(_fileInfos)} is null, make sure to run {nameof(DownloadFilesRequest)} first");
        if (_mod == null)
            throw new ArgumentNullException($"{nameof(_mod)} is null, make sure to run {nameof(DownloadFilesRequest)} first");
        if (_map == null)
            throw new ArgumentNullException($"{nameof(_map)} is null, make sure to run {nameof(DownloadFilesRequest)} first");

        var hasMod = false;
        var hasMap = false;

        foreach (var fileInfo in _fileInfos)
        {
            if (fileInfo.Mod.ToLower() == _mod.ToLower())
            {
                hasMod = true;
                if (fileInfo.FileType == Bf1942FileTypes.Level && Path.GetFileNameWithoutExtension(fileInfo.FileNameWithoutPatchNumber).ToLower() == _map.ToLower())
                    hasMap = true;
            }
        }

        _logger.LogDebug($"VerifyFileList — hasMod={hasMod}, hasMap={hasMap}.");
        return (hasMod, hasMap);
    }

    /// <summary>Step 3 in synchronizing files.</summary>
    public async Task DownloadFilesDownload(DownloadBackgroundWorker backgroundWorkerTotal, DownloadBackgroundWorker backgroundWorkerCurrentFile, CancellationToken cancellationToken)
    {
        // TODO: add to protocol a way to tell the client that it does not have mod or map, maybe dont do this in download manager but in a new manager (downloadCheck)

        if (_fileInfos == null)
            throw new ArgumentNullException($"{nameof(_fileInfos)} is null, make sure to run {nameof(DownloadFilesRequest)} first");

        List<DownloadBackgroundWorker> backgroundWorkers = new() { backgroundWorkerTotal, backgroundWorkerCurrentFile };

        var fileInfosOfFilesToDownload = _fileInfos.Where(x => x.SyncType == SyncType.Download);
        var numberOfFilesExpected = fileInfosOfFilesToDownload.Count();
        var totalSizeExpected = fileInfosOfFilesToDownload.Sum(x => x.Size);

        _logger.LogInformation($"Starting download of {numberOfFilesExpected} files, total size {totalSizeExpected} bytes.");

        var responses = new List<string>();
        foreach (var fileInfo in _fileInfos)
            responses.Add(fileInfo.SyncType == SyncType.Download ? "yes" : "no");

        _communication.SendString(string.Join(' ', responses));

        var data = await _communication.ReceiveSpaceSeperatedString(3); // acknowledgement numberOfFiles totalSize

        var numberOfFiles = int.Parse(data.ElementAt(1));
        if (numberOfFiles != numberOfFilesExpected)
            throw new Exception($"numberOfFiles: {numberOfFiles} != {numberOfFilesExpected}");

        var totalSize = ulong.Parse(data.ElementAt(2));
        if (totalSize != totalSizeExpected)
            throw new Exception($"totalSize: {totalSize} != {totalSizeExpected}");

        _communication.SendAcknowledgement();

        int fileIndex = 0;
        foreach (var fileInfoOfFileToDownload in fileInfosOfFilesToDownload)
        {
            cancellationToken.ThrowIfCancellationRequested();
            fileIndex++;

            var fileInfo = await _communication.ReceiveFileInfo();
            if (!fileInfo.IsEqualTo(fileInfoOfFileToDownload))
                throw new Exception("File info sent right before file download does not match the agreed file info sequence");

            _logger.LogDebug($"Downloading file {fileIndex}/{numberOfFilesExpected}: {fileInfo.FilePath} ({fileInfo.Size} bytes).");

            backgroundWorkerCurrentFile.TotalSize = fileInfo.Size;
            _communication.SendAcknowledgement();
            var filePath = _localFileCacheManager.GetWorkingDirectoryFilePath(fileInfo);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "");

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await _communication.ReceiveFile(fileInfo.Size, fileStream, backgroundWorkers, cancellationToken);

            _communication.SendAcknowledgement();
            fileStream.Close();
            FileHelper.SetLastWriteTime(filePath, fileInfo.LastModifiedTimestamp);

            _logger.LogDebug($"File {fileInfo.FilePath} downloaded and saved.");
        }

        _communication.SendAcknowledgement();
        _communication.Dispose();
        _logger.LogInformation($"All {numberOfFilesExpected} files downloaded successfully.");
    }

    /// <summary>Step 4 in synchronizing files.</summary>
    public void DownloadFilesWrapUp()
    {
        if (_fileInfos == null)
            throw new ArgumentNullException($"{nameof(_fileInfos)} is null, make sure to run {nameof(DownloadFilesRequest)} first");

        _logger.LogDebug("Verifying checksums and sizes of downloaded files.");

        var fileInfosOfFilesToDownload = _fileInfos.Where(x => x.SyncType == SyncType.Download);

        // check if all downloaded files have the correct checksum and file size:
        foreach (var fileInfoOfFileToDownload in fileInfosOfFilesToDownload)
        {
            var filePathInWorkingDirectory = $"{_localFileCacheManager.WorkingDirectoryWithSlash}mods/{fileInfoOfFileToDownload.Mod}/{fileInfoOfFileToDownload.FilePath}";
            var fileInfo = new FileInfo(filePathInWorkingDirectory, _localFileCacheManager.WorkingDirectory);
            if (fileInfo.Checksum != fileInfoOfFileToDownload.Checksum)
                throw new Exception($"Downloaded file has incorrect checksum: {fileInfo.Checksum}. Expected: {fileInfoOfFileToDownload.Checksum}");
            if (fileInfo.Size != fileInfoOfFileToDownload.Size)
                throw new Exception($"Downloaded file has incorrect size: {fileInfo.Size}. Expected: {fileInfoOfFileToDownload.Size}");
        }

        _logger.LogDebug("Checksums verified. Moving files from cache to working directory.");
        var fileInfosOfFilesInCache = _fileInfos.Where(x => x.SyncType == SyncType.LocalFileCache);
        _localFileCacheManager.MoveFilesFromCacheToWorkingDirectory(fileInfosOfFilesInCache);

        _logger.LogDebug("Moving files from game to cache directory.");
        var fileInfoGroups = FileInfoGroup.GetFileInfoGroups(_fileInfos);
        _localFileCacheManager.MoveFilesFromGameToCacheDirectory(fileInfoGroups);

        _logger.LogDebug("Moving files from working directory to game directory.");
        _localFileCacheManager.EmptyWorkingDirectoryIntoGameDirectory();
        _localFileCacheManager.RemoveWorkingDirectory();

        _logger.LogInformation("File sync wrap-up complete.");
    }
}
