using Microsoft.Extensions.Logging;
using System.Reflection;

public class UpdateManager
{
    public static Version Version { get; } = Assembly.GetExecutingAssembly().GetName().Version;

    private readonly DataField42Communication _communication;
    private readonly ILogger<UpdateManager> _logger;

    private const string UpdaterFileName = "DataField42_updater.exe";

    public UpdateManager(DataField42Communication communication, ILogger<UpdateManager> logger)
    {
        _communication = communication;
        _logger = logger;
    }

    public async Task Update(DownloadBackgroundWorker backgroundWorker, CancellationToken cancellationToken, string restartArguments)
    {
        _logger.LogInformation($"Starting update from version {Version}.");
        _communication.StartSession();
        _communication.SendString($"update {Version}");
        var fileSize = await _communication.ReceiveUlong();
        backgroundWorker.TotalSize = fileSize;
        _logger.LogDebug($"Updater file size: {fileSize} bytes.");

        // TODO: check file size
        _communication.SendAcknowledgement();

        using (var fileStream = new FileStream(UpdaterFileName, FileMode.Create, FileAccess.Write))
        {
            await _communication.ReceiveFile(fileSize, fileStream, backgroundWorker, cancellationToken);
        }
        _communication.SendAcknowledgement();

        _logger.LogInformation($"Update download complete. Launching updater with args: {restartArguments}.");
        ExternalProcess.SwitchTo(UpdaterFileName, arguments: restartArguments);
    }

    public async Task<Version> RequestVersion()
    {
        _logger.LogDebug("Requesting server version.");
        (_, var version) = await _communication.HandShake(Version);
        _logger.LogDebug($"Server version: {version}.");
        return version;
    }
}
