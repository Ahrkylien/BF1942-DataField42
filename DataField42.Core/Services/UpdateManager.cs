using System.IO;

public class UpdateManager
{
    public static Version Version { get; } = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

    private readonly DataField42Communication _communication;

    private const string UpdaterFileName = "DataField42_updater.exe";

    public UpdateManager(DataField42Communication communication)
    {
        _communication = communication;
    }

    public async Task Update(DownloadBackgroundWorker backgroundWorker, CancellationToken cancellationToken)
    {
        _communication.StartSession();
        _communication.SendString($"update {Version}");
        var fileSize = await _communication.ReceiveUlong();
        backgroundWorker.TotalSize = fileSize;

        // TODO: check file size
        _communication.SendAcknowledgement();

        using (var fileStream = new FileStream(UpdaterFileName, FileMode.Create, FileAccess.Write))
        {
            await _communication.ReceiveFile(fileSize, fileStream, backgroundWorker, cancellationToken);
        }
        _communication.SendAcknowledgement();

        ExternalProcess.SwitchTo(UpdaterFileName, arguments: string.Join(" ", Environment.GetCommandLineArgs()[1..]));
    }

    public async Task<Version> RequestVersion() => await RequestVersion(_communication);

    public static async Task<Version> RequestVersion(DataField42Communication communication)
    {
        communication.StartSession();
        communication.SendString($"handshake {Version}");
        return new Version(await communication.ReceiveString());
    }
}
