using System.Diagnostics;
using System.Text;

public class Bf1942Client(string path)
{
    public void Start(string? modId = null, string? ipPort = null, string? password = null)
    {
        if (Debugger.IsAttached)
            Environment.Exit(0);

        string arguments = " +restart 1"; // is space required?
        if (modId != null) 
            arguments += $" +game {modId}";
        if (ipPort != null) 
            arguments += $" +joinServer {ipPort}";
        else 
            arguments += $" +goToInterface 6";
        if (password != null) 
            arguments += $" +password  {password}";

        ExternalProcess.SwitchTo(path, arguments);
    }

    public bool IsDataField42PatchApplied()
    {
        if (Debugger.IsAttached)
            return true;

        try
        {
            using var clientExe = new FileStream(path, FileMode.Open, FileAccess.Read);

            foreach (var (offset, bytes) in Bf1942ClientPatches.Patches)
            {
                var buffer = new byte[bytes.Length];

                clientExe.Seek(offset, SeekOrigin.Begin);

                if (clientExe.Read(buffer, 0, buffer.Length) != buffer.Length)
                    throw new Exception($"Failed to read client exe.");

                if (!buffer.SequenceEqual(bytes))
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed checking BF1942.exe patch status: " + ex.Message, ex);
        }
    }

    public void ApplyDataField42Patch()
    {
        try
        {
            using var clientExe = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
            foreach (var (offset, bytes) in Bf1942ClientPatches.Patches)
            {
                clientExe.Seek(offset, SeekOrigin.Begin);
                clientExe.Write(bytes, 0, bytes.Length);
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Failed Patching BF1942.exe: " + ex.Message, ex);
        }
    }

    public string GetKeyRegistryPath()
    {
        var bytes = Read(0x4C52D0, 100);
        int nullIndex = Array.IndexOf(bytes, (byte)0x00);
        return Encoding.UTF8.GetString(bytes[..nullIndex]);
    }

    private byte[] Read(int offset, int length)
    {
        try
        {
            var buffer = new byte[length];
            using var clientExe = new FileStream(path, FileMode.Open, FileAccess.Read);
            clientExe.Seek(offset, SeekOrigin.Begin);
            clientExe.ReadExactly(buffer, 0, length);
            return buffer;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed Patching BF1942.exe: " + ex.Message, ex);
        }
    }
}
