using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DataField42.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _messages = "Welcome to DataField42";

    [ObservableProperty]
    private int _percentage;

    [ObservableProperty]
    private bool _continueToDownloadStage;

    [ObservableProperty]
    private bool _downloadStage;

    [ObservableProperty]
    private bool _returnToGameStage;

    [ObservableProperty]
    private bool _returnToGameStageCheckboxVisible;

    private DownloadManager? _downloadManager;
    private ulong _totalSizeExpected;

    public MainWindowViewModel()
    {
        Task.Run(async () => PrepareDownload());
    }

    private async Task PrepareDownload()
    {
        try
        {
#if DEBUG
            CommandLineArguments.Parse(new[] { "", "map", "SOFTWARE\\test", "1.1.1.1:14567", "", "bf1942/levels/berlin/", "bf1942" });
#else
    CommandLineArguments.Parse(args);
#endif
        }
        catch (Exception e)
        {
            PostError($"Can't parse command line arguments: {e.Message}");
        }

        if (CommandLineArguments.Identifier == CommandLineArgumentIdentifier.DownloadAndJoinServer)
        {
            PostMessage($"Map: {CommandLineArguments.Map}, Mod: {CommandLineArguments.Mod}");
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
            catch (TimeoutException e)
            {
                PostMessage($"Can't connect to central database. It seems to be down...");
            }
            catch (Exception e)
            {
                PostMessage($"Can't connect to central database: {e.Message}");
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
                PostMessage($"Failed updating client: {e.Message}");
                Environment.Exit(0);
            }

            // Try to connect to server:
            DataField42Communication? communicationWithServer = null;
            var connectedToServer = false;
            try
            {
                communicationWithServer = new DataField42Communication(CommandLineArguments.Ip);
                var serverVersion = UpdateManager.RequestVersion(communicationWithServer);
                if (serverVersion != UpdateManager.Version)
                    throw new Exception($"Server has wrong version running: {serverVersion}");
                connectedToServer = true;
            }
            catch (TimeoutException e)
            {
                PostMessage($"Server doesn't have DataField42");
            }
            catch (Exception e)
            {
                PostMessage($"Can't connect to server: {e.Message}");
            }

            // Check if server has DataField42 otherwise use central db:
            var readyToDownload = true;
            if (!connectedToServer)
            {
                if (connectedToMaster && communicationWithMaster != null)
                {
                    communicationWithServer = communicationWithMaster;
                    PostMessage($"Resolved to central database.");
                }
                else
                {
                    PostMessage($"And cannot resolve to central database...");
                    readyToDownload = false;
                }
            }

            if (readyToDownload && communicationWithServer != null)
            {
                ISyncRuleManager syncRuleManager = new SyncRuleManager("Synchronization rules.txt");
                ILocalFileCacheManager localFileCacheManager = new LocalFileCacheManager("cache", "tmp", "game");
                var downloadDecisionMaker = new DownloadDecisionMaker(syncRuleManager, localFileCacheManager);
                _downloadManager = new DownloadManager(communicationWithServer, downloadDecisionMaker, localFileCacheManager);
                PostMessage("test 1");
                // ToDo: create cache of crc values in game folder
                var fileInfos = _downloadManager.DownloadFilesRequest(CommandLineArguments.Mod, CommandLineArguments.Map, CommandLineArguments.Ip, CommandLineArguments.Port, CommandLineArguments.KeyHash);
                PostMessage("test 2");
                var fileInfosOfFilesToDownload = fileInfos.Where(x => x.SyncType == SyncType.Download);
                var numberOfFilesExpected = fileInfosOfFilesToDownload.Count();
                _totalSizeExpected = fileInfosOfFilesToDownload.Sum(x => x.Size);
                PostMessage($"DataField42 wants to download {numberOfFilesExpected} files which is a total of {_totalSizeExpected.ToReadableFileSize()}, from {communicationWithServer.DisplayName}");
                ContinueToDownloadStage = true;
                // TODO: check rules for auto yes. or ask for yes from user
            }
        }
        else
        {
            PostMessage($"Unknown Command Line Argument Identifier {CommandLineArguments.Identifier}");
        }

        if (!ContinueToDownloadStage)
            EnterReturnToGameStage(allowAutomaticJoin: false);
    }

    [RelayCommand]
    private void Download() {
        ContinueToDownloadStage = false;
        DownloadStage = true;
        Task.Run(async () =>
        {
            bool downloadSuccessful = false;
            try
            {
                // TODO: add file synctype represent absence of file (now it can be included in the download list)
                // TODO: make all file sizes ulong

                var backgroundWorkerTotal = new DownloadBackgroundWorker(_totalSizeExpected);
                var backgroundWorkerCurrentFile = new DownloadBackgroundWorker(0);
                backgroundWorkerTotal.ProgressChanged += BackgroundWorkerCurrentFile_ProgressChanged;
                //backgroundWorkerCurrentFile.ProgressChanged += BackgroundWorkerCurrentFile_ProgressChanged;
                _downloadManager.DownloadFilesDownload(backgroundWorkerTotal, backgroundWorkerCurrentFile);
                _downloadManager.DownloadFilesWrapUp();
                downloadSuccessful = true;
            }
            catch (Exception ex)
            {
                PostError($"Failed to Download Files: {ex.Message}");
            }
            EnterReturnToGameStage(downloadSuccessful);
        });
    }

    private void EnterReturnToGameStage(bool joinServer = false, bool allowAutomaticJoin = true)
    {
        // TODO: Think of a way to check if the central db / server has the map.
        // Then think of how to procede. (join mod, not server if map not found)
        ReturnToGameStage = true;
        ReturnToGameStageCheckboxVisible = allowAutomaticJoin;
    }

    [RelayCommand]
    private void ReturnBackToGame()
    {
#if !DEBUG
        Bf1942Client.Start(CommandLineArguments.Mod, $"{CommandLineArguments.Ip}:{CommandLineArguments.Port}", CommandLineArguments.Password);
#else
        Environment.Exit(0);
#endif
    }

    private void PostMessage(string message)
    {
        Messages += $"\n{message}";
    }

    private void PostError(string message) => PostMessage(message);

    private void BackgroundWorkerCurrentFile_ProgressChanged(int percentage)
    {
        Percentage = percentage;
    }
}
