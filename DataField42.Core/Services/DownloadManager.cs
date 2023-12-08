using System.IO;

public class DownloadManager
{
    private readonly DataField42Communication _communication;
    private readonly DownloadDecisionMaker _downloadDecisionMaker;
    private readonly ILocalFileCacheManager _localFileCacheManager;

    private string? _mod;
    private string? _map;

    private List<FileInfo>? _fileInfos;

    public DownloadManager(DataField42Communication dataField42Communication, DownloadDecisionMaker downloadDecisionMaker, ILocalFileCacheManager localFileCacheManager)
    {
        _communication = dataField42Communication;
        _downloadDecisionMaker = downloadDecisionMaker;
        _localFileCacheManager = localFileCacheManager;
    }

    /// <summary>
    /// Step 1 in synchronizing files
    /// </summary>
    public async Task<IEnumerable<FileInfo>> DownloadFilesRequest(string mod, string map, string ip, int port, string keyHash, CancellationToken cancellationToken)
    {
        _mod = mod;
        _map = map;

        _localFileCacheManager.RemoveWorkingDirectory();
        _communication.StartSession();
        _communication.SendString($"download {map} {mod} {ip} {port} {keyHash}");

        _fileInfos = await _communication.ReceiveFileInfos(cancellationToken);

        // TODO: better messaging for double files in list 
        // TODO: check for absense of base rfa


        if (_fileInfos.Count > 100) // TODO: check a resonable max
            throw new Exception($"Server wants to sync {_fileInfos.Count} files which is more than 100");

        _downloadDecisionMaker.CheckDownloadRequests(_fileInfos);
        return _fileInfos;
    }

    /// <summary>
    /// Step 2 in synchronizing files
    /// </summary>
    /// <returns>hasMod, hasMap</returns>
    public (bool, bool) VerifyFileList()
    {
        if (_fileInfos == null)
            throw new ArgumentNullException($"{nameof(_fileInfos)} is null, make sure to run {nameof(DownloadFilesRequest)} first");
        if (_mod == null)
            throw new ArgumentNullException($"{nameof(_mod)} is null, make sure to run {nameof(DownloadFilesRequest)} first");
        if (_map == null)
            throw new ArgumentNullException($"{nameof(_map)} is null, make sure to run {nameof(DownloadFilesRequest)} first");

        var hasMod = false;
        var hasMap= false;

        foreach (var fileInfo in _fileInfos)
        {
            if (fileInfo.Mod.ToLower() == _mod.ToLower())
            {
                hasMod = true;
                if (fileInfo.FileType == Bf1942FileTypes.Level && Path.GetFileNameWithoutExtension(fileInfo.FileNameWithoutPatchNumber).ToLower() == _map.ToLower())
                {
                    hasMap = true;
                }
            }
        }
        
        return (hasMod, hasMap);
    }

    /// <summary>
    /// Step 3 in synchronizing files
    /// </summary>
    public async Task DownloadFilesDownload(DownloadBackgroundWorker backgroundWorkerTotal, DownloadBackgroundWorker backgroundWorkerCurrentFile, CancellationToken cancellationToken)
    {
        // TODO: add to protocol a way to tell the client that it does not have mod or map, maybe dont do this in download manager but in a new manager (downloadCheck)

        if (_fileInfos == null)
            throw new ArgumentNullException($"{nameof(_fileInfos)} is null, make sure to run {nameof(DownloadFilesRequest)} first");

        List<DownloadBackgroundWorker> backgroundWorkers = new() { backgroundWorkerTotal, backgroundWorkerCurrentFile };

        var fileInfosOfFilesToDownload = _fileInfos.Where(x => x.SyncType == SyncType.Download);
        var numberOfFilesExpected = fileInfosOfFilesToDownload.Count();
        var totalSizeExpected = fileInfosOfFilesToDownload.Sum(x => x.Size);

        var responses = new List<string>();
        foreach (var fileInfo in _fileInfos)
            responses.Add(fileInfo.SyncType == SyncType.Download ? "yes" : "no");

        _communication.SendString(string.Join(' ', responses));

        var data = await _communication.ReceiveSpaceSeperatedString(3); // acknowledgement numberOfFiles totalSize

        var numberOfFiles = int.Parse(data.ElementAt(1));
        if (numberOfFiles != numberOfFilesExpected)
            throw new Exception($"numberOfFiles: {numberOfFiles} != {numberOfFilesExpected} ");

        var totalSize = ulong.Parse(data.ElementAt(2));
        if (totalSize != totalSizeExpected)
            throw new Exception($"totalSize: {totalSize} != {totalSizeExpected} ");


        _communication.SendAcknowledgement();

        foreach (var fileInfoOfFileToDownload in fileInfosOfFilesToDownload)
        {
            // Check for cancellation before processing each file
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = await _communication.ReceiveFileInfo();
            if (!fileInfo.IsEqualTo(fileInfoOfFileToDownload))
                throw new Exception("File info send right before file download does not match the file info sequence on which was agreed");

            backgroundWorkerCurrentFile.TotalSize = fileInfo.Size;
            _communication.SendAcknowledgement();
            var filePath = _localFileCacheManager.GetWorkingDirectoryFilePath(fileInfo);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "");

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);

            // Check for cancellation while downloading the file
            await _communication.ReceiveFile(fileInfo.Size, fileStream, backgroundWorkers, cancellationToken);

            _communication.SendAcknowledgement();
            fileStream.Close();
            FileHelper.SetLastWriteTime(filePath, fileInfo.LastModifiedTimestamp);
        }
        _communication.SendAcknowledgement();
        _communication.Dispose();
    }

    /// <summary>
    /// Step 4 in synchronizing files
    /// </summary>
    public void DownloadFilesWrapUp()
    {
        if (_fileInfos == null)
            throw new ArgumentNullException($"{nameof(_fileInfos)} is null, make sure to run {nameof(DownloadFilesRequest)} first");

        var fileInfosOfFilesToDownload = _fileInfos.Where(x => x.SyncType == SyncType.Download);

        // check if all downloaded files have the correct checksum and file size:
        foreach (var fileInfoOfFileToDownload in fileInfosOfFilesToDownload)
        {
            var filePathInWorkingDirectory = $"{_localFileCacheManager.WorkingDirectoryWithSlash}mods/{fileInfoOfFileToDownload.Mod}/{fileInfoOfFileToDownload.FilePath}";
            var fileInfo = new FileInfo(filePathInWorkingDirectory, _localFileCacheManager.WorkingDirectory);
            if (fileInfo.Checksum != fileInfoOfFileToDownload.Checksum)
                throw new Exception($"Downloaded file has incorrect checksum: {fileInfo.Checksum}. It should be: {fileInfoOfFileToDownload.Checksum}");
            if (fileInfo.Size != fileInfoOfFileToDownload.Size)
                throw new Exception($"Downloaded file has incorrect size: {fileInfo.Size}. It should be: {fileInfoOfFileToDownload.Size}");
        }

        // move files from cache to working dir:
        // TODO: copying from cache to tmp can be done async while downloading
        var fileInfosOfFilesInCache = _fileInfos.Where(x => x.SyncType == SyncType.LocalFileCache);
        _localFileCacheManager.MoveFilesFromCacheToWorkingDirectory(fileInfosOfFilesInCache);

        // move files from game to cache:
        List<FileInfoGroup> fileInfoGroups = FileInfoGroup.GetFileInfoGroups(_fileInfos);
        _localFileCacheManager.MoveFilesFromGameToCacheDirectory(fileInfoGroups);

        // move files from working dir to game:
        _localFileCacheManager.EmptyWorkingDirectoryIntoGameDirectory();

        _localFileCacheManager.RemoveWorkingDirectory();
    }
}