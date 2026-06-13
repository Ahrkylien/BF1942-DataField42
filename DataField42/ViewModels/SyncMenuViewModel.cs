using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Interfaces;
using DataField42.Settings;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace DataField42.ViewModels;

public partial class SyncMenuViewModel : ObservableObject, IPageViewModel
{
    public string Title => "File Synchronization";

    [ObservableProperty]
    private int _percentage;

    [ObservableProperty]
    private bool _continueToDownloadStage;

    [ObservableProperty]
    private bool _downloadStage;

    [ObservableProperty]
    private bool _returnToGameStage;

    [ObservableProperty]
    private bool _autoSyncServerCheckBox;

    [ObservableProperty]
    private bool _autoJoinServerCheckBox;

    [ObservableProperty]
    private bool _autoJoinServerCheckboxVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessages))]
    [NotifyPropertyChangedFor(nameof(HasMessagesOrErrors))]
    private string _messages = string.Empty;
    public bool HasMessages => !string.IsNullOrEmpty(Messages);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessages))]
    [NotifyPropertyChangedFor(nameof(HasMessagesOrErrors))]
    private string _errorMessages = string.Empty;
    public bool HasErrorMessages => !string.IsNullOrEmpty(ErrorMessages);
    public bool HasMessagesOrErrors => HasMessages || HasErrorMessages;

    private readonly ILogger<SyncMenuViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private DataField42Communication? _communicationWithServer;
    private ISyncRuleManager _syncRuleManager;
    private DownloadManager? _downloadManager;
    private ulong _totalSizeExpected;
    private bool _joinServerWhenReturnToGame = false;
    private readonly SyncParameters _syncParameters;
    private readonly Bf1942Client _bf1942Client;
    private readonly Bf1942ServerLobby _serverLobby;
    private readonly CancellationTokenSource _cancelationTokenSource = new();

    public SyncMenuViewModel(
        SettingsService settingsService,
        SyncParameters syncParameters,
        Bf1942Client bf1942Client,
        Bf1942ServerLobby serverLobby,
        ILoggerFactory loggerFactory)
    {
        _syncRuleManager = new SyncRuleManager(settingsService);
        _syncParameters = syncParameters;
        _bf1942Client = bf1942Client;
        _serverLobby = serverLobby;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<SyncMenuViewModel>();

        _logger.LogInformation($"SyncMenuViewModel created for {syncParameters.Ip}:{syncParameters.Port}, mod={syncParameters.Mod}, map={syncParameters.Map}.");

        Task.Run(async () => await PrepareDownload());
    }

    private async Task PrepareDownload()
    {
        _logger.LogInformation($"PrepareDownload started for mod={_syncParameters.Mod}, map={_syncParameters.Map}.");
        var stageSuccessful = false;
        PostMessage($"Map: {_syncParameters.Map}, Mod: {_syncParameters.Mod}");

        if (_syncParameters.Map == "*")
        {
            _logger.LogDebug("Map is wildcard — querying master server for current map.");
            PostMessage($"Querying master server to get query port...");
            try
            {
                await _serverLobby.GetServerListFromHttpApi();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get server list from master API.");
                PostError($"Can't get server list: {ex.Message}");
            }
            await _serverLobby.QueryAllServers();

            int port = 23000;
            foreach (var server in _serverLobby.Servers)
            {
                if (server.QueryResult != null && server.Ip.ToString() == _syncParameters.Ip && server.QueryResult.HostPort == _syncParameters.Port)
                {
                    port = server.QueryPort;
                    break;
                }
            }

            _logger.LogDebug($"Querying server {_syncParameters.Ip}:{port} for current map.");
            PostMessage($"Querying server to get current map: {_syncParameters.Ip}:{port}");
            var serverQuery = new Bf1942ServerQuery(IPAddress.Parse(_syncParameters.Ip), port);
            try
            {
                var queryResult = await serverQuery.Query(TimeSpan.FromMilliseconds(2000));
                var map = queryResult.MapName.Replace(' ', '_');

                if (!(Regex.IsMatch(map, $"^[{FileInfo.AllowableChars}]*$") && map.Length >= 1)) // only letters digits and underscores and hyphens and at least 1 char
                    throw new ArgumentException($"Server has sent an illegal map name: {map}");

                _syncParameters.Map = map;
                _logger.LogInformation($"Resolved map from server: {map}.");
            }
            catch (TimeoutException)
            {
                _logger.LogWarning($"Server {_syncParameters.Ip}:{port} did not respond in time when querying for map.");
                PostError($"Server did not respond in time");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error querying server {_syncParameters.Ip}:{port} for map.");
                PostError(ex.Message);
                return;
            }

            PostMessage($"Map: {_syncParameters.Map}, Mod: {_syncParameters.Mod}");
        }

        // TODO: Try to connect to master and server paralel, but since master is always up it might not be much extra performance
        // Try to connect to master:
        DataField42Communication? communicationWithMaster = null;
        UpdateManager? updateManager = null;
        Version masterVersion = new Version();
        var connectedToMaster = false;
        try
        {
            _logger.LogDebug("Connecting to central database.");
            communicationWithMaster = new DataField42Communication(_loggerFactory.CreateLogger<DataField42Communication>());
            updateManager = new UpdateManager(communicationWithMaster, _loggerFactory.CreateLogger<UpdateManager>());
            masterVersion = await updateManager.RequestVersion();
            connectedToMaster = true;
            _logger.LogInformation($"Connected to central database. Master version: {masterVersion}.");
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timed out connecting to central database.");
            PostError($"Can't connect to central database. It seems to be down...");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to connect to central database.");
            PostError($"Can't connect to central database: {e.Message}");
        }

        // Update client if master is alive and client is behind:
        var hasCorrectVersion = true;
        try
        {
            if (connectedToMaster && updateManager != null)
            {
                if (masterVersion > UpdateManager.Version)
                {
                    _logger.LogInformation($"Client update required: {masterVersion} > {UpdateManager.Version}.");
                    PostMessage($"Starting to update to version: {masterVersion}");
                    var backgroundWorker = new DownloadBackgroundWorker();
                    await updateManager.Update(backgroundWorker, _cancelationTokenSource.Token, _syncParameters.ToString());
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Client update failed.");
            PostError($"Failed updating client: {ex.Message}");
            hasCorrectVersion = false;
        }

        if (hasCorrectVersion)
        {
            // Try to connect to server:
            var connectedToServer = false;
            try
            {
                _logger.LogDebug($"Connecting to server {_syncParameters.Ip}.");
                _communicationWithServer = new DataField42Communication(_syncParameters.Ip, _loggerFactory.CreateLogger<DataField42Communication>());
                (var redirectServerIp, var serverVersion) = await _communicationWithServer.HandShake(UpdateManager.Version);

                if (redirectServerIp != null)
                {
                    _logger.LogInformation($"Server redirected to {redirectServerIp}.");
                    PostMessage($"Server redirected to {redirectServerIp}");
                    _communicationWithServer = new DataField42Communication(redirectServerIp, _loggerFactory.CreateLogger<DataField42Communication>());
                    (redirectServerIp, serverVersion) = await _communicationWithServer.HandShake(UpdateManager.Version);
                    if (redirectServerIp != null)
                        throw new InvalidOperationException($"Server tries to redirect leading to a second redirect which is not allowed.");
                }

                if (serverVersion.Major != UpdateManager.Version.Major || serverVersion.Minor != UpdateManager.Version.Minor)
                    throw new Exception($"Server has wrong version running: {serverVersion}");

                connectedToServer = true;
                _logger.LogInformation($"Connected to server {_syncParameters.Ip}, version {serverVersion}.");
            }
            catch (TimeoutException)
            {
                _logger.LogInformation($"Server {_syncParameters.Ip} does not have DataField42.");
                PostMessage($"Server doesn't have DataField42");
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to connect to server {_syncParameters.Ip}.");
                PostError($"Can't connect to server ({_syncParameters.Ip}): {e.Message}");
            }

            // Check if server has DataField42 otherwise use central db:
            var readyToDownload = true;
            if (!connectedToServer)
            {
                if (connectedToMaster && communicationWithMaster != null)
                {
                    _communicationWithServer = communicationWithMaster;
                    _logger.LogInformation("Falling back to central database for file sync.");
                    PostMessage($"Resolved to central database.");
                }
                else
                {
                    _logger.LogError("Server unreachable and central database unavailable — cannot sync.");
                    PostError($"And cannot resolve to central database...");
                    readyToDownload = false;
                }
            }

            if (readyToDownload && _communicationWithServer != null)
            {
                try
                {
                    ILocalFileCacheManager localFileCacheManager = new LocalFileCacheManager("DataField42/cache", "DataField42/tmp", "", _loggerFactory.CreateLogger<LocalFileCacheManager>());
                    var downloadDecisionMaker = new DownloadDecisionMaker(_syncRuleManager, localFileCacheManager, _loggerFactory.CreateLogger<DownloadDecisionMaker>());
                    _downloadManager = new DownloadManager(_communicationWithServer, downloadDecisionMaker, localFileCacheManager, _loggerFactory.CreateLogger<DownloadManager>());

                    PostMessage("DataField42 is calculating files..");
                    _logger.LogDebug("Requesting file list from server/db.");
                    var fileInfos = await _downloadManager.DownloadFilesRequest(
                        _syncParameters.Mod,
                        _syncParameters.Map,
                        _syncParameters.Ip,
                        _syncParameters.Port,
                        _syncParameters.KeyHash,
                        _cancelationTokenSource.Token);

                    (var hasMod, var hasMap) = _downloadManager.VerifyFileList();
                    var fileInfosOfFilesToDownload = fileInfos.Where(x => x.SyncType == SyncType.Download);
                    var numberOfFilesExpected = fileInfosOfFilesToDownload.Count();
                    _totalSizeExpected = fileInfosOfFilesToDownload.Sum(x => x.Size);

                    if (!hasMod)
                    {
                        _logger.LogWarning($"Server/db does not have mod: {_syncParameters.Mod}.");
                        PostError($"Server doesn't have the mod: {_syncParameters.Mod}");
                    }
                    else if (!hasMap && _syncParameters.Map != "*")
                    {
                        _logger.LogWarning($"Server/db does not have map: {_syncParameters.Map}.");
                        PostError($"Server doesn't have the map: {_syncParameters.Map}");
                    }
                    else if (numberOfFilesExpected == 0)
                    {
                        _logger.LogInformation("All files already synchronized. No download required.");
                        _downloadManager.DownloadFilesWrapUp();
                        PostMessage("All files are already synchronized");
                        stageSuccessful = true;
                        EnterReturnToGameStage(joinServer: true, _syncRuleManager.IsAutoJoinEnabled());
                    }
                    else
                    {
                        _logger.LogInformation($"Pending download: {numberOfFilesExpected} files, {_totalSizeExpected} bytes total from {_communicationWithServer.DisplayName}.");
                        PostMessage($"DataField42 wants to download {numberOfFilesExpected} files which is a total of {_totalSizeExpected.ToReadableFileSize()}, from {_communicationWithServer.DisplayName}");
                        stageSuccessful = true;
                        if (_syncRuleManager.IsAutoSyncEnabled(_communicationWithServer.DisplayName))
                            Download();
                        else
                            ContinueToDownloadStage = true;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error during file request/decision phase.");
                    PostError($"Error: {ex.Message}");
                }
            }
        }

        // For now we set joinServer: true, because we want to join the server even when the central DB is offline
        if (!stageSuccessful)
        {
            _logger.LogWarning("Prepare download stage did not succeed. Proceeding to return-to-game stage.");
            EnterReturnToGameStage(joinServer: true, automaticJoinServer: _syncRuleManager.IsAutoJoinEnabled());
        }
    }

    [RelayCommand]
    private void Download()
    {
        ContinueToDownloadStage = false;
        DownloadStage = true;
        // TODO : Add cancel button
        Task.Run(async () =>
        {
            _logger.LogInformation("Download stage started.");
            bool downloadSuccessful = false;
            bool automaticJoinServer = false;
            try
            {
                if (_syncRuleManager == null || _communicationWithServer == null || _downloadManager == null)
                    throw new Exception($"Illegal start of Download stage. _syncRuleManager: {_syncRuleManager != null}, _communicationWithServer: {_communicationWithServer != null}, _downloadManager: {_downloadManager != null}");

                if (AutoSyncServerCheckBox)
                {
                    _logger.LogDebug($"Auto-sync enabled for {_communicationWithServer.DisplayName}.");
                    _syncRuleManager.AutoSyncEnable(_communicationWithServer.DisplayName);
                }

                // TODO: add file synctype represent absence of file (now it can be included in the download list)

                var backgroundWorkerTotal = new DownloadBackgroundWorker(_totalSizeExpected);
                var backgroundWorkerCurrentFile = new DownloadBackgroundWorker(0);
                backgroundWorkerTotal.ProgressChanged += BackgroundWorkerCurrentFile_ProgressChanged;

                await _downloadManager.DownloadFilesDownload(backgroundWorkerTotal, backgroundWorkerCurrentFile, _cancelationTokenSource.Token);
                _downloadManager.DownloadFilesWrapUp();

                automaticJoinServer = _syncRuleManager.IsAutoJoinEnabled();
                downloadSuccessful = true;
                _logger.LogInformation("Download stage completed successfully.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Download stage failed.");
                PostError($"Failed to Download Files: {ex.Message}");
            }

            EnterReturnToGameStage(downloadSuccessful, automaticJoinServer);
        });
    }

    private void EnterReturnToGameStage(bool joinServer = false, bool automaticJoinServer = false)
    {
        _logger.LogDebug($"Entering return-to-game stage. joinServer={joinServer}, autoJoin={automaticJoinServer}.");
        _joinServerWhenReturnToGame = joinServer;

        if (joinServer && automaticJoinServer)
            ReturnBackToGame();

        AutoJoinServerCheckboxVisible = _joinServerWhenReturnToGame;
        ReturnToGameStage = true;

        // TODO: Think of a way to check if the central db / server has the map.
        // Then think of how to procede. (join mod, not server if map not found)
    }

    [RelayCommand]
    private void ReturnBackToGame()
    {
        _logger.LogInformation($"Returning to game. joinServer={_joinServerWhenReturnToGame}, autoJoinCheckbox={AutoJoinServerCheckBox}.");

        if (AutoJoinServerCheckBox)
        {
            if (_syncRuleManager == null)
                throw new Exception($"Illegal start of 'Return back to game' stage. {nameof(_syncRuleManager)}: {_syncRuleManager != null}");

            _syncRuleManager.AutoJoinEnable();
        }

        if (_joinServerWhenReturnToGame)
            _bf1942Client.Start(_syncParameters.Mod, $"{_syncParameters.Ip}:{_syncParameters.Port}", _syncParameters.Password);
        else
            _bf1942Client.Start(_syncParameters.Mod);

    }

    private void PostMessage(string message)
    {
        _logger.LogInformation($"[SyncMenu] {message}");
        message = ">> " + message;
        if (HasMessages)
            message = $"\n{message}";
        Messages += message;
    }

    private void PostError(string message)
    {
        _logger.LogError($"[SyncMenu] {message}");
        message = ">> " + message;
        if (HasErrorMessages)
            message = $"\n{message}";
        ErrorMessages += message;
    }

    private void BackgroundWorkerCurrentFile_ProgressChanged(int percentage)
    {
        Percentage = percentage;
    }

    public Task LeavePage()
    {
        _logger.LogDebug("SyncMenuViewModel leaving page — cancelling operations.");
        _cancelationTokenSource.Cancel();
        _communicationWithServer?.Dispose();
        return Task.CompletedTask;
    }
}
