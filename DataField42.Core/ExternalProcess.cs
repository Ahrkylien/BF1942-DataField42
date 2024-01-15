using System.IO;
using System.Diagnostics;

public static class ExternalProcess
{
    public static void SwitchTo(string path, string? arguments = null, bool adminMode = false)
    {
        if (arguments == null)
            arguments = string.Empty;

        var processStartInfo = new ProcessStartInfo(path, arguments);
        processStartInfo.UseShellExecute = true;
        //processStartInfo.WindowStyle = ProcessWindowStyle.Normal;
        processStartInfo.WorkingDirectory = Path.GetDirectoryName(path);

        if (adminMode)
            processStartInfo.Verb = "runas";

        _ = Process.Start(processStartInfo) ?? throw new Exception("Process resource can't be started");
        Environment.Exit(0);
    }
}
