using DataField42.Settings;
using DataField42.ViewModels;
using DataField42.Views;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace DataField42;

public partial class App : Application
{
#pragma warning disable CS8618
    private Microsoft.Extensions.Logging.ILogger _logger;
#pragma warning restore CS8618

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!IsAdministrator() && IsInAdminDirectory())
            ExternalProcess.SwitchTo(Environment.ProcessPath, adminMode: true);

        // Startup logger after admin mode has atained (if needed).
        SetupSerilog();
        var loggerFactory = LoggerFactory.Create(b => b.AddSerilog(dispose: true));
        _logger = loggerFactory.CreateLogger<App>();

        _logger.LogInformation($"Application starting. Version: {UpdateManager.Version}.");

        var settingsService = new SettingsService("DataField42/Settings.ini");
        var bf1942Client = new Bf1942Client("BF1942.exe");
        var mainWindowViewModel = new MainWindowViewModel(settingsService, bf1942Client, loggerFactory);

        var mainWindow = new MainWindow(mainWindowViewModel);
        mainWindow.Show();
        MainWindow = mainWindow;

        Task.Run(() => RunStartupUpdate(loggerFactory));
    }

    private static void SetupSerilog()
    {
        Directory.CreateDirectory("DataField42/Logs");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: "DataField42/Logs/DataField42.log",
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u4} {SourceContext}: {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
    }

    private async Task RunStartupUpdate(ILoggerFactory loggerFactory)
    {
        _logger.LogDebug("Checking for application updates.");
        try
        {
            var communication = new DataField42Communication(loggerFactory.CreateLogger<DataField42Communication>());
            var updateManager = new UpdateManager(communication, loggerFactory.CreateLogger<UpdateManager>());
            var masterVersion = await updateManager.RequestVersion();

            if (masterVersion > UpdateManager.Version)
            {
                _logger.LogInformation($"Update available: {masterVersion}. Downloading.");
                var backgroundWorker = new DownloadBackgroundWorker();
                await updateManager.Update(backgroundWorker, CancellationToken.None, CommandLineArguments.RawString ?? "");
            }
            else
            {
                _logger.LogDebug("Application is up to date.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup update check failed silently.");
        }
    }

    private static bool IsAdministrator() =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    private static bool IsInAdminDirectory()
    {
        const string testFileName = "DataField42 - admin test";
        try
        {
            File.WriteAllText(testFileName, "This is a test.");
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        finally
        {
            File.Delete(testFileName);
        }
        return false;
    }
}
