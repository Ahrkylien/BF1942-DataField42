using System.IO;
using System.Reflection;

public class LocalFileCacheManager : ILocalFileCacheManager
{
    public string ChacheDirectory { get; init; }
    public string WorkingDirectory { get; init; }
    public string GameDirectory { get; init; }

    public string ChacheDirectoryWithSlash => (ChacheDirectory == "" ? "" : ChacheDirectory + "/");
    public string WorkingDirectoryWithSlash => (WorkingDirectory == "" ? "" : WorkingDirectory + "/");
    public string GameDirectoryWithSlash => (GameDirectory == "" ? "" : GameDirectory + "/");

    public LocalFileCacheManager(string chacheDirectory, string workingDirectory, string gameDirectory)
    {
        ChacheDirectory = chacheDirectory;
        WorkingDirectory = workingDirectory;
        GameDirectory = gameDirectory;
    }

    /// <summary>
    /// Check if file exist in game folder.
    /// Other versions of this file are allowed.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns></returns>
    public bool CheckIfSimilarFileExistsInGame(FileInfo fileInfo)
    {
        var filePath = $"{GameDirectoryWithSlash}mods/{fileInfo.Mod}/{fileInfo.Directory}{(fileInfo.Directory == "" ? "" : "/")}{fileInfo.FileNameWithoutPatchNumber}";

        return File.Exists(filePath);
    }
        
    public bool CheckIfFileExistsInGame(FileInfo fileInfo)
    { 
        if (fileInfo.RepresentsAbsenceOfFile)
            return true;
        
        var filePath = GetGameDirectoryFilePath(fileInfo);

        if (!File.Exists(filePath))
            return false;

        var fileInfoLocalFile = new FileInfo(filePath, GameDirectory);
        
        return fileInfoLocalFile.Checksum == fileInfo.Checksum && fileInfoLocalFile.Size == fileInfo.Size;
    }

    public bool CheckIfFileExistsInCache(FileInfo fileInfo)
    { 
        if (fileInfo.RepresentsAbsenceOfFile)
            return true;
        
        var filePath = GetCachedFilePath(fileInfo);

        if (!File.Exists(filePath))
            return false;

        var fileInfoLocalFile = new FileInfo(filePath, ChacheDirectory, fromCache: true);

        // Double check that the file has this Crc
        // if it makes it too slow another approach should be taken
        if (!(fileInfoLocalFile.Checksum == fileInfo.Checksum && fileInfoLocalFile.Size == fileInfo.Size))
            throw new Exception($"Unexpected file in file cache: {filePath}, expected crc: {fileInfo.Checksum} & size: {fileInfo.Size}");
        
        return true;
    }

    public void MoveFilesFromCacheToWorkingDirectory(IEnumerable<FileInfo> fileInfos)
    {
        foreach (var fileInfo in fileInfos)
        {
            if (!fileInfo.RepresentsAbsenceOfFile)
            {
                var filePathInWorkingDirectory = GetWorkingDirectoryFilePath(fileInfo);
                FileHelper.MoveAndCreateDirectory(GetCachedFilePath(fileInfo), filePathInWorkingDirectory);
            }
        }   
    }

    /// <summary>
    /// Move all files matching the FileInfoGroup.
    /// It might be that there are no files, this is allowed.
    /// </summary>
    /// <param name="fileInfoGroup"></param>
    public void MoveFilesFromGameToCacheDirectory(IEnumerable<FileInfoGroup> fileInfoGroups)
    {
        foreach (var fileInfoGroup in fileInfoGroups)
        {
            // if rfa move all files that match the name and patch criteria also if RepresentsAbsenceOfFile
            if (fileInfoGroup.FileType == Bf1942FileTypes.Level || fileInfoGroup.FileType == Bf1942FileTypes.Archive)
            {
                string[] filesToCheck = Array.Empty<string>();
                try
                {
                    filesToCheck = Directory.GetFiles($"{GameDirectoryWithSlash}mods/{fileInfoGroup.Mod}/{fileInfoGroup.Directory}", $"{Path.GetFileNameWithoutExtension(fileInfoGroup.FileNameWithoutPatchNumber)}*.rfa");
                }catch (DirectoryNotFoundException) { } // swallow this exception
                
                foreach (var filePathToCheck in filesToCheck)
                {
                    var fileInfoLocalFile = new FileInfo(filePathToCheck, GameDirectory, fast: true);

                    // continue to next iteration if file should stay in game dir:
                    var fileInfoFromGroup = fileInfoGroup.FileInfos.Where(x => x.FilePath.ToLower() == fileInfoLocalFile.FilePath.ToLower());
                    if (fileInfoFromGroup.Any()) // should be one though
                    {
                        fileInfoLocalFile = fileInfoFromGroup.ElementAt(0);
                        if (fileInfoLocalFile.SyncType == SyncType.LocalFile)
                            continue;
                    }
                    else // get with checksum for cache:
                    {
                        fileInfoLocalFile = new FileInfo(filePathToCheck, GameDirectory);
                    }

                    if (fileInfoGroup.FileNameWithoutPatchNumber.ToLower() == fileInfoLocalFile.FileNameWithoutPatchNumber.ToLower())
                    {
                        var filePathInCache = GetCachedFilePath(fileInfoLocalFile);
                        FileHelper.MoveAndCreateDirectory(filePathToCheck, filePathInCache, true);
                    }
                }
            }
            else
            {
                if (fileInfoGroup.FileInfos[0].SyncType == SyncType.LocalFileCache || fileInfoGroup.FileInfos[0].SyncType == SyncType.Download)
                {
                    var filePath = GetGameDirectoryFilePath(fileInfoGroup.FileInfos[0]);
                    if (File.Exists(filePath))
                    {
                        var filePathInCache = GetCachedFilePath(fileInfoGroup.FileInfos[0]);
                        FileHelper.MoveAndCreateDirectory(filePath, filePathInCache, true);
                    }
                }
            }
        }
    }

    public void EmptyWorkingDirectoryIntoGameDirectory()
    {
        if (Directory.Exists(WorkingDirectory))
        {
            string[] filePaths = Directory.GetFiles(WorkingDirectory, "*", SearchOption.AllDirectories);
            foreach (var filePath in filePaths)
            {
                var correctedFilePath = filePath.Replace('\\', '/');
                var fileInfoLocalFile = new FileInfo(correctedFilePath, WorkingDirectory, fast: true);
                var filePathInGameDirectory = GetGameDirectoryFilePath(fileInfoLocalFile);
                FileHelper.MoveAndCreateDirectory(correctedFilePath, filePathInGameDirectory);
            }
        }
    }

    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(WorkingDirectory))
        {
            var dir = new DirectoryInfo(WorkingDirectory);
            dir.Delete(true);
        }
    }

    public string GetCachedFilePath(FileInfo fileInfo) => $"{ChacheDirectoryWithSlash}mods/{fileInfo.Mod}/{fileInfo.Directory}{(fileInfo.Directory == "" ? "" : "/")}{fileInfo.Checksum} {fileInfo.FileName}";

    public string GetWorkingDirectoryFilePath(FileInfo fileInfo) => $"{WorkingDirectoryWithSlash}mods/{fileInfo.Mod}/{fileInfo.FilePath}";

    public string GetGameDirectoryFilePath(FileInfo fileInfo) => $"{GameDirectoryWithSlash}mods/{fileInfo.Mod}/{fileInfo.FilePath}";
}

