﻿using System.IO;

public interface ILocalFileCacheManager
{
    bool CheckIfFileExistsInGame(FileInfo fileInfo);
    bool CheckIfFileExistsInCache(FileInfo fileInfo);
    void MoveFilesFromCacheToWorkingDirectory(IEnumerable<FileInfo> fileInfos);
    void MoveFilesFromGameToCacheDirectory(IEnumerable<FileInfoGroup> fileInfoGroup);
    void EmptyWorkingDirectoryIntoGameDirectory();

    void RemoveWorkingDirectory();

    string GetCachedFilePath(FileInfo fileInfo);
    string GetWorkingDirectoryFilePath(FileInfo fileInfo);
    string GetGameDirectoryFilePath(FileInfo fileInfo);
}

public class LocalFileCacheManager : ILocalFileCacheManager
{
    private string _chacheDirectory;
    private string _workingDirectory;
    private string _gameDirectory;

    public LocalFileCacheManager(string chacheDirectory, string workingDirectory, string gameDirectory)
    {
        _chacheDirectory = chacheDirectory;
        _workingDirectory = workingDirectory;
        _gameDirectory = gameDirectory;
    }

    public bool CheckIfFileExistsInGame(FileInfo fileInfo) { 
        if (fileInfo.RepresentsAbsenceOfFile)
            return true;
        
        var filePath = GetGameDirectoryFilePath(fileInfo);

        if (!File.Exists(filePath))
            return false;

        var fileInfoLocalFile = new FileInfo(filePath, _gameDirectory);
        
        return fileInfoLocalFile.Checksum == fileInfo.Checksum && fileInfoLocalFile.Size == fileInfo.Size;
    }

    public bool CheckIfFileExistsInCache(FileInfo fileInfo) { 
        if (fileInfo.RepresentsAbsenceOfFile)
            return true;
        
        var filePath = GetCachedFilePath(fileInfo);

        if (!File.Exists(filePath))
            return false;

        var fileInfoLocalFile = new FileInfo(filePath, _chacheDirectory, fromCache: true);

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
                string[] filesToCheck = { };
                try
                {
                    filesToCheck = Directory.GetFiles($"{_gameDirectory}/mods/{fileInfoGroup.Mod}/{fileInfoGroup.Directory}", $"{fileInfoGroup.RfaNameLower}*.rfa");
                }catch (DirectoryNotFoundException) { } // swallow this exception
                
                foreach (var filePathToCheck in filesToCheck)
                {
                    var fileInfoLocalFile = new FileInfo(filePathToCheck, _gameDirectory, fast: true);

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
                        fileInfoLocalFile = new FileInfo(filePathToCheck, _gameDirectory);
                    }

                    if (fileInfoGroup.RfaNameLower == fileInfoLocalFile.RfaNameLower)
                    {
                        var filePathInCache = GetCachedFilePath(fileInfoLocalFile);
                        FileHelper.MoveAndCreateDirectory(filePathToCheck, filePathInCache, true);
                    }
                }
            }
            else
            {
                if (!(fileInfoGroup.FileInfos[0].SyncType == SyncType.LocalFile)) {
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
        if (Directory.Exists(_workingDirectory))
        {
            string[] filePaths = Directory.GetFiles(_workingDirectory, "*", SearchOption.AllDirectories);
            foreach (var filePath in filePaths)
            {
                var correctedFilePath = filePath.Replace('\\', '/');
                var fileInfoLocalFile = new FileInfo(correctedFilePath, _workingDirectory, fast: true);
                var filePathInGameDirectory = GetGameDirectoryFilePath(fileInfoLocalFile);
                FileHelper.MoveAndCreateDirectory(correctedFilePath, filePathInGameDirectory);
            }
        }
    }

    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_workingDirectory))
        {
            var dir = new DirectoryInfo(_workingDirectory);
            dir.Delete(true);
        }
    }

    public string GetCachedFilePath(FileInfo fileInfo) => $"{_chacheDirectory}/mods/{fileInfo.Mod}/{fileInfo.Directory}{(fileInfo.Directory == "" ? "" : "/")}{fileInfo.Checksum} {fileInfo.FileName}";

    public string GetWorkingDirectoryFilePath(FileInfo fileInfo) => $"{_workingDirectory}/mods/{fileInfo.Mod}/{fileInfo.FilePath}";

    public string GetGameDirectoryFilePath(FileInfo fileInfo) => $"{_gameDirectory}/mods/{fileInfo.Mod}/{fileInfo.FilePath}";
}

