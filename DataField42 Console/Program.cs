var loadBarLength = 0;
var maxLoadBarLength = 40;
bool hasHadFirstProgressUpdate = false;

Console.WriteLine("Console version of DataField42");

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
    Console.WriteLine($"Can't parse command line arguments: {e.Message}");
    Environment.Exit(0);
}



if (CommandLineArguments.Identifier == CommandLineArgumentIdentifier.DownloadAndJoinServer)
{
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
        Console.WriteLine($"Can't connect to central database. It seems to be down...");
    }
    catch (Exception e)
    {
        Console.WriteLine($"Can't connect to central database: {e.Message}");
    }

    // Update client if master is alive and client is behind:
    try
    {
        if (connectedToMaster && updateManager != null)
        {
            if (masterVersion != UpdateManager.Version)
            {
                Console.WriteLine($"Starting to update to version: {masterVersion}");
                var backgroundWorker = new DownloadBackgroundWorker();
                updateManager.Update(backgroundWorker);
            }
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"Failed updating client: {e.Message}");
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
        Console.WriteLine($"Server doesn't have DataField42");
    }
    catch (Exception e)
    {
        Console.WriteLine($"Can't connect to server: {e.Message}");
    }

    // Check if server has DataField42 otherwise use central db:
    var readyToDownload = true;
    if (!connectedToServer)
    {
        if (connectedToMaster && communicationWithMaster != null)
        {
            communicationWithServer = communicationWithMaster;
            Console.WriteLine($"Resolved to central database.");
        }
        else
        {
            Console.WriteLine($"And cannot resolve to central database...");
            readyToDownload = false;
        }
    }

    if (readyToDownload && communicationWithServer != null) {
        ISyncRuleManager syncRuleManager = new SyncRuleManagerDummy();
        ILocalFileCacheManager localFileCacheManager = new LocalFileCacheManager("cache", "tmp", "game");
        var downloadDecisionMaker = new DownloadDecisionMaker(syncRuleManager, localFileCacheManager);
        var downloadManager = new DownloadManager(communicationWithServer, downloadDecisionMaker, localFileCacheManager);
        Console.WriteLine("test 1");
        // ToDo: create cache of crc values in game folder
        var fileInfos = downloadManager.DownloadFilesRequest(CommandLineArguments.Mod, CommandLineArguments.Map, CommandLineArguments.Ip, CommandLineArguments.Port, CommandLineArguments.KeyHash);
        Console.WriteLine("test 2");
        var fileInfosOfFilesToDownload = fileInfos.Where(x => x.SyncType == SyncType.Download);
        var numberOfFilesExpected = fileInfosOfFilesToDownload.Count();
        ulong totalSizeExpected = fileInfosOfFilesToDownload.Sum(x => x.Size);
        Console.WriteLine($"DataField42 wants to download {numberOfFilesExpected} files which is a total of {totalSizeExpected.ToReadableFileSize()}, from {communicationWithServer.DisplayName}");

        Console.WriteLine($"Do you want to continue??");
        // TODO: check rules for auto yes. or ask for yes from user
        // TODO: add file synctype represent absence of file (now it can be included in the download list)

        var backgroundWorkerTotal = new DownloadBackgroundWorker(totalSizeExpected);
        var backgroundWorkerCurrentFile = new DownloadBackgroundWorker(0);
        backgroundWorkerTotal.ProgressChanged += BackgroundWorkerCurrentFile_ProgressChanged;
        //backgroundWorkerCurrentFile.ProgressChanged += BackgroundWorkerCurrentFile_ProgressChanged;
        downloadManager.DownloadFilesDownload(backgroundWorkerTotal, backgroundWorkerCurrentFile);
        downloadManager.DownloadFilesWrapUp();
#if !DEBUG
        Bf1942Client.Start(CommandLineArguments.Mod, $"{CommandLineArguments.Ip}:{CommandLineArguments.Port}", CommandLineArguments.Password);
#endif
    }

    Console.WriteLine($"Rejoining game...");
#if !DEBUG
    Bf1942Client.Start(CommandLineArguments.Mod);
#endif
}
else
{
    Console.WriteLine($"Unknown Command Line Argument Identifier {CommandLineArguments.Identifier}");
}

void BackgroundWorkerCurrentFile_ProgressChanged(int percentage)
{
    if (loadBarLength == 0 && !hasHadFirstProgressUpdate)
    {
        Console.WriteLine(new string('-', maxLoadBarLength));
        hasHadFirstProgressUpdate = true;
    }
    var newLoadBarLength = percentage * maxLoadBarLength / 100;
    if (newLoadBarLength > loadBarLength)
    {
        Console.Write(new string('#', newLoadBarLength - loadBarLength));
        loadBarLength = newLoadBarLength;
    }
    if (percentage == 100)
    {
        Console.Write($"\n");
        hasHadFirstProgressUpdate = false;
        loadBarLength = 0;
    }
}