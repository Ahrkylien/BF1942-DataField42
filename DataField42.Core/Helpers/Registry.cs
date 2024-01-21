using Microsoft.Win32;

public static class Registry
{
    public static string ReadKey(string subKey)
    {
#pragma warning disable CA1416 // Validate platform compatibility
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subKey, false);
        string keyValue = key?.GetValue(null)?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(keyValue))
            throw new Exception($"Cant find key at: {subKey}, value: {keyValue}");
        return keyValue;
#pragma warning restore CA1416 // Validate platform compatibility
    }
}