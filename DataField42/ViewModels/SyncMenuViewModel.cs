using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Interfaces;

namespace DataField42.ViewModels;
public partial class SyncMenuViewModel : ObservableObject, IPageViewModel
{
    [ObservableProperty]
    private string _messages = "Welcome to DataField42";

    [ObservableProperty]
    private string _errorMessages = "";

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

    private MainWindowViewModel _mainWindowViewModel;
    private DataField42Communication? _communicationWithServer;
    private ISyncRuleManager? _syncRuleManager;
    private DownloadManager? _downloadManager;
    private ulong _totalSizeExpected;
    private bool _joinServerWhenReturnToGame = false;
    private readonly SyncParameters _syncParameters;

    public SyncMenuViewModel(MainWindowViewModel mainWindowViewModel, SyncParameters syncParameters)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _syncParameters = syncParameters;
        Task.Run(async () => PrepareDownload());
    }

    private async Task PrepareDownload()
    {
        var stageSuccessful = false;
        PostMessage($"Map: {_syncParameters.Map}, Mod: {_syncParameters.Mod}");
        // TODO: Try to connect to master and server paralel, but since master is always up it might not be much extra performance
        // Try to connect to master:
        DataField42Communication? communicationWithMaster = null;
        UpdateManager? updateManager = null;
        int masterVersion = 0;
        var connectedToMaster = false;
        try
        {
            communicationWithMaster = communicationWithMaster = new DataField42Communication();
            updateManager = new UpdateManager(communicationWithMaster);
            masterVersion = updateManager.RequestVersion();
            connectedToMaster = true;
        }
        catch (TimeoutException)
        {
            PostError($"Can't connect to central database. It seems to be down...");
        }
        catch (Exception e)
        {
            PostError($"Can't connect to central database: {e.Message}");
        }

        // Update client if master is alive and client is behind:
        try
        {
            if (connectedToMaster && updateManager != null)
            {
                if (masterVersion > UpdateManager.Version)
                {
                    PostMessage($"Starting to update to version: {masterVersion}");
                    var backgroundWorker = new DownloadBackgroundWorker();
                    updateManager.Update(backgroundWorker);
                }
            }
        }
        catch (Exception e)
        {
            PostError($"Failed updating client: {e.Message}");
            Environment.Exit(0);
        }

        // Try to connect to server:
        var connectedToServer = false;
        try
        {
            _communicationWithServer = new DataField42Communication(_syncParameters.Ip);
            var serverVersion = UpdateManager.RequestVersion(_communicationWithServer);
            if (serverVersion != UpdateManager.Version)
                throw new Exception($"Server has wrong version running: {serverVersion}");
            connectedToServer = true;
        }
        catch (TimeoutException)
        {
            PostMessage($"Server doesn't have DataField42");
        }
        catch (Exception e)
        {
            PostError($"Can't connect to server ({_syncParameters.Ip}): {e.Message}");
        }

        // Check if server has DataField42 otherwise use central db:
        var readyToDownload = true;
        if (!connectedToServer)
        {
            if (connectedToMaster && communicationWithMaster != null)
            {
                _communicationWithServer = communicationWithMaster;
                PostMessage($"Resolved to central database.");
            }
            else
            {
                PostError($"And cannot resolve to central database...");
                readyToDownload = false;
            }
        }

        if (readyToDownload && _communicationWithServer != null)
        {
            try
            {
                _syncRuleManager = new SyncRuleManager("DataField42/Synchronization rules.txt");
                ILocalFileCacheManager localFileCacheManager = new LocalFileCacheManager("DataField42/cache", "DataField42/tmp", "");
                var downloadDecisionMaker = new DownloadDecisionMaker(_syncRuleManager, localFileCacheManager);
                _downloadManager = new DownloadManager(_communicationWithServer, downloadDecisionMaker, localFileCacheManager);
                _mainWindowViewModel.DisplayMessage("Server is calculating files..");
                var fileInfos = _downloadManager.DownloadFilesRequest(_syncParameters.Mod, _syncParameters.Map, _syncParameters.Ip, _syncParameters.Port, _syncParameters.KeyHash);
                (var hasMod, var hasMap) = _downloadManager.VerifyFileList();
                var fileInfosOfFilesToDownload = fileInfos.Where(x => x.SyncType == SyncType.Download);
                var numberOfFilesExpected = fileInfosOfFilesToDownload.Count();
                _totalSizeExpected = fileInfosOfFilesToDownload.Sum(x => x.Size);

                if (!hasMod)
                    PostError($"Server doesn't have the mod: {_syncParameters.Mod}");
                else if (!hasMap && _syncParameters.Map != "*") // TODO: download map right away (not only mod)
                    PostError($"Server doesn't have the map: {_syncParameters.Map}");
                else if (numberOfFilesExpected == 0)
                    PostMessage("TODO: go to wrap up stage without downoad display");
                    // EnterReturnToGameStage(joinServer: true);
                else
                {
                    PostMessage($"DataField42 wants to download {numberOfFilesExpected} files which is a total of {_totalSizeExpected.ToReadableFileSize()}, from {_communicationWithServer.DisplayName}");
                    stageSuccessful = true;
                    if (_syncRuleManager.IsAutoSyncEnabled(_communicationWithServer.DisplayName))
                        Download();
                    else
                        ContinueToDownloadStage = true;
                }
            }
            catch (Exception ex)
            {
                PostError($"Error: {ex.Message}");
            }
        }

        if (!stageSuccessful)
            EnterReturnToGameStage(joinServer: false);
    }

    [RelayCommand]
    private void Download() {
        ContinueToDownloadStage = false;
        DownloadStage = true;
        // TODO : Add cancel button
        Task.Run(async () =>
        {
            bool downloadSuccessful = false;
            bool automaticJoinServer = false;
            try
            {
                if (_syncRuleManager == null || _communicationWithServer == null || _downloadManager == null)
                    throw new Exception($"Illegal start of Download stage. {nameof(_syncRuleManager)}: {_syncRuleManager != null}, {nameof(_communicationWithServer)}: {_communicationWithServer != null}, {nameof(_downloadManager)}: {_downloadManager != null}");

                if (AutoSyncServerCheckBox)
                    _syncRuleManager.AutoSyncEnable(_communicationWithServer.DisplayName);
                
                // TODO: add file synctype represent absence of file (now it can be included in the download list)

                var backgroundWorkerTotal = new DownloadBackgroundWorker(_totalSizeExpected);
                var backgroundWorkerCurrentFile = new DownloadBackgroundWorker(0);
                backgroundWorkerTotal.ProgressChanged += BackgroundWorkerCurrentFile_ProgressChanged;
                //backgroundWorkerCurrentFile.ProgressChanged += BackgroundWorkerCurrentFile_ProgressChanged;
                _downloadManager.DownloadFilesDownload(backgroundWorkerTotal, backgroundWorkerCurrentFile);
                _downloadManager.DownloadFilesWrapUp();
                automaticJoinServer = _syncRuleManager.IsAutoJoinEnabled();
                downloadSuccessful = true;
            }
            catch (Exception ex)
            {
                PostError($"Failed to Download Files: {ex.Message}");
            }
            EnterReturnToGameStage(downloadSuccessful, automaticJoinServer);
        });
    }

    private void EnterReturnToGameStage(bool joinServer = false, bool automaticJoinServer = false)
    {
        _joinServerWhenReturnToGame = joinServer;
        if (joinServer)
        {
            if (automaticJoinServer)
            {
                ReturnBackToGame();
            }
            else
            {
                ReturnToGameStage = true;
                AutoJoinServerCheckboxVisible = true;
            }
        }
        else
        {
            ReturnToGameStage = true;
            AutoJoinServerCheckboxVisible = false;
        }
        // TODO: Think of a way to check if the central db / server has the map.
        // Then think of how to procede. (join mod, not server if map not found)
    }

    [RelayCommand]
    private void ReturnBackToGame()
    {
        if (AutoJoinServerCheckBox)
        {
            if (_syncRuleManager == null)
                throw new Exception($"Illegal start of 'Return back to game' stage. {nameof(_syncRuleManager)}: {_syncRuleManager != null}");

            _syncRuleManager.AutoJoinEnable();
        }
#if !DEBUG
        if (_joinServerWhenReturnToGame)
            Bf1942Client.Start(_syncParameters.Mod, $"{_syncParameters.Ip}:{_syncParameters.Port}", _syncParameters.Password);
        else
            Bf1942Client.Start(_syncParameters.Mod);
#else
        Environment.Exit(0);
#endif
    }

    private void PostMessage(string message) => _mainWindowViewModel.DisplayMessage(message);
    private void PostError(string message) => _mainWindowViewModel.DisplayError(message);

    private void BackgroundWorkerCurrentFile_ProgressChanged(int percentage)
    {
        Percentage = percentage;
    }
}
