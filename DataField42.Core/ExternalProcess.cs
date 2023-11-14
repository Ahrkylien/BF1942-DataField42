using System.IO;
using System.Diagnostics;

public static class ExternalProcess
{
    public static void SwitchTo(string path, string? arguments = null)
    {
        if (arguments == null)
            arguments = string.Empty;

        var processStartInfo = new ProcessStartInfo(path, arguments);
        processStartInfo.UseShellExecute = true;
        //processStartInfo.WindowStyle = ProcessWindowStyle.Normal;
        processStartInfo.WorkingDirectory = Path.GetDirectoryName(path);
        _ = Process.Start(processStartInfo) ?? throw new Exception("Process recourse can't be started");
        Environment.Exit(0);
    }
}
