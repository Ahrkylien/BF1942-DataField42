using System.IO;

public class UpdateManager
{
    public static int Version { get; } = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.Major ?? 0;

    private readonly DataField42Communication _communication;

    private const string UpdaterFileName = "DataField42_updater.exe";

    public UpdateManager(DataField42Communication communication)
    {
        _communication = communication;
    }

    public void Update(DownloadBackgroundWorker backgroundWorker)
    {
        _communication.StartSession();
        _communication.SendString($"update {Version}");
        var fileSize = _communication.ReceiveUlong();
        backgroundWorker.TotalSize = fileSize;

        // TODO: check file size
        _communication.SendAcknowledgement();

        using var fileStream = new FileStream(UpdaterFileName, FileMode.Create, FileAccess.Write);
        _communication.ReceiveFile(fileSize, fileStream, backgroundWorker);

        ExternalProcess.SwitchTo(UpdaterFileName);
    }

    public int RequestVersion() => RequestVersion(_communication);

    public static int RequestVersion(DataField42Communication communication)
    {
        communication.StartSession();
        communication.SendString($"handshake {Version}");
        return communication.ReceiveInt();
    }
}
