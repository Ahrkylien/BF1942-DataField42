﻿public interface ILocalFileCacheManager
{
    bool CheckIfSimilarFileExistsInGame(FileInfo fileInfo);
    bool CheckIfFileExistsInGame(FileInfo fileInfo);
    bool CheckIfFileExistsInCache(FileInfo fileInfo);
    void MoveFilesFromCacheToWorkingDirectory(IEnumerable<FileInfo> fileInfos);
    void MoveFilesFromGameToCacheDirectory(IEnumerable<FileInfoGroup> fileInfoGroup);
    void EmptyWorkingDirectoryIntoGameDirectory();

    void RemoveWorkingDirectory();

    string GetCachedFilePath(FileInfo fileInfo);
    string GetWorkingDirectoryFilePath(FileInfo fileInfo);
    string GetGameDirectoryFilePath(FileInfo fileInfo);

    string WorkingDirectory { get; }
    string WorkingDirectoryWithSlash { get; }
}