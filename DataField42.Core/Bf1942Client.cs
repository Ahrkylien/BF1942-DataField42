public static class Bf1942Client
{
    public static void Start(string? modId = null, string? ipPort = null, string? password = null, string path = "BF1942.exe")
    {
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
}
