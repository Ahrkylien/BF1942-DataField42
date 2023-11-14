public delegate void FileDownloadProgressChangedEventHandler(int percentage);

public class DownloadBackgroundWorker
{
    public ulong TotalSize;
    private ulong _totalDownloadedSize = 0;
    public event FileDownloadProgressChangedEventHandler? ProgressChanged;

    public DownloadBackgroundWorker(ulong totalSize = 0)
    {
        TotalSize = totalSize;
    }

    public void ReportProgressPercentage(int progressPercentage)
    {
        ProgressChanged?.Invoke(progressPercentage);
    }

    public void ReportProgressAmount(ulong amountDownloaded)
    {
        _totalDownloadedSize += amountDownloaded;
        ReportProgressPercentage((int)(100 * _totalDownloadedSize / TotalSize));
    }

}
