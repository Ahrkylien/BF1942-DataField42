using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace DataField42;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!IsAdministrator() && IsInAdminDirectory())
            ExternalProcess.SwitchTo(Process.GetCurrentProcess().MainModule.FileName, adminMode: true);
    }

    public static bool IsAdministrator() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public static bool IsInAdminDirectory()
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

