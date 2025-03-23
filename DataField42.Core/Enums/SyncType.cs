public enum SyncType
{
    Unknown,
    /// <summary>
    /// This is used when no sync should occur on the entire FileInfoGroup.
    /// </summary>
    None,
    LocalFile,
    LocalFileCache,
    Download,
}
