using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows;

namespace DataField42;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!IsAdministrator() && IsInAdminDirectory())
            ExternalProcess.SwitchTo(Process.GetCurrentProcess().MainModule.FileName, adminMode: true);
        Task.Run(Update);
    }

    private async Task Update()
    {
        try
        {
            var communicationWithMaster = new DataField42Communication();
            var updateManager = new UpdateManager(communicationWithMaster);
            var masterVersion = await updateManager.RequestVersion();
            if (masterVersion > UpdateManager.Version)
            {
                var backgroundWorker = new DownloadBackgroundWorker();
                await updateManager.Update(backgroundWorker, CancellationToken.None, CommandLineArguments.RawString ?? "");
            }
        }
        catch { }
    }

    private static bool IsAdministrator() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    private static bool IsInAdminDirectory()
    {
        string FileSystemName = "DataField42 - admin test";
        try
        {
            // Attempt to create a test file in the folder
            File.WriteAllText(FileSystemName, "This is a test.");
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        finally
        {
            File.Delete(FileSystemName);
        }
        return false;
    }
}

