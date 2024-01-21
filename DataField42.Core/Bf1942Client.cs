﻿using System.IO;
using System.Text;

public static class Bf1942Client
{
    public const string Path = "BF1942.exe";

    public static void Start(string? modId = null, string? ipPort = null, string? password = null, string path = Path)
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

    public static void ApplyDataField42Patch()
    {
        (int, byte[])[] patches =
            {
                (0x9494F, new byte[] { 0xE8, 0xBC, 0x75, 0xF7, 0xFF, 0x90, 0x90 }),
                (0x0BF10, new byte[] { 0x8D, 0x8C, 0x24, 0x6C, 0xFF, 0xFF, 0xFF, 0x55, 0x50, 0x53, 0x51, 0x52, 0x57, 0x56, 0x89, 0xE9, 0x89, 0xE5, 0x81, 0xEC, 0x00, 0x03, 0x00, 0x00, 0x6A, 0x00, 0xA1, 0xAC, 0x1E, 0x97, 0x00, 0x8D, 0x88, 0xD4, 0x07, 0x00, 0x00, 0xFF, 0x15, 0xDC, 0x30, 0x8C, 0x00, 0x50, 0x8D, 0x46, 0x0C, 0x50, 0x8D, 0x8F, 0xD4, 0x02, 0x00, 0x00, 0xFF, 0x15, 0xDC, 0x30, 0x8C, 0x00, 0x89, 0xC1, 0x31, 0xDB, 0xC6, 0x85, 0x00, 0xFE, 0xFF, 0xFF, 0x22, 0x8A, 0x04, 0x19, 0x43, 0x88, 0x84, 0x1D, 0x00, 0xFE, 0xFF, 0xFF, 0x84, 0xC0, 0x75, 0xF1, 0xC6, 0x84, 0x1D, 0x00, 0xFE, 0xFF, 0xFF, 0x22, 0xC6, 0x84, 0x1D, 0x01, 0xFE, 0xFF, 0xFF, 0x00, 0x8D, 0x85, 0x00, 0xFE, 0xFF, 0xFF, 0x50, 0x8D, 0x8F, 0x2C, 0x02, 0x00, 0x00, 0xFF, 0x15, 0xDC, 0x30, 0x8C, 0x00, 0x50, 0x31, 0xDB, 0xC6, 0x85, 0x00, 0xFF, 0xFF, 0xFF, 0x22, 0x8A, 0x83, 0xD0, 0x52, 0x8C, 0x00, 0x43, 0x88, 0x84, 0x1D, 0x00, 0xFF, 0xFF, 0xFF, 0x84, 0xC0, 0x75, 0xEE, 0xC6, 0x84, 0x1D, 0x00, 0xFF, 0xFF, 0xFF, 0x22, 0xC6, 0x84, 0x1D, 0x01, 0xFF, 0xFF, 0xFF, 0x00, 0x8D, 0x85, 0x00, 0xFF, 0xFF, 0xFF, 0x50, 0x68, 0xF0, 0xBF, 0x40, 0x00, 0x68, 0xE0, 0xBF, 0x40, 0x00, 0x68, 0xE0, 0xBF, 0x40, 0x00, 0x6A, 0x02, 0xFF, 0x15, 0xF8, 0x34, 0x8C, 0x00, 0x83, 0xC4, 0x14, 0x89, 0xEC, 0x5E, 0x5F, 0x5A, 0x59, 0x5B, 0x58, 0x5D, 0xC3, 0x44, 0x61, 0x74, 0x61, 0x46, 0x69, 0x65, 0x6C, 0x64, 0x34, 0x32, 0x2E, 0x65, 0x78, 0x65, 0x00, 0x6D, 0x61, 0x70, 0x00 }),
                (0x94EF5, new byte[] { 0xE8, 0xF6, 0x71, 0xF7, 0xFF, 0x90, 0x90, 0x90, 0x90, 0x90 }),
                (0x0C0F0, new byte[] { 0xC7, 0x87, 0xB0, 0x02, 0x00, 0x00, 0x0B, 0x00, 0x00, 0x00, 0x55, 0x50, 0x53, 0x51, 0x52, 0x57, 0x56, 0x89, 0xE9, 0x89, 0xE5, 0x81, 0xEC, 0x00, 0x02, 0x00, 0x00, 0x6A, 0x00, 0x56, 0x8D, 0x8F, 0xD4, 0x02, 0x00, 0x00, 0xFF, 0x15, 0xDC, 0x30, 0x8C, 0x00, 0x89, 0xC1, 0x31, 0xDB, 0xC6, 0x85, 0x00, 0xFE, 0xFF, 0xFF, 0x22, 0x8A, 0x04, 0x19, 0x43, 0x88, 0x84, 0x1D, 0x00, 0xFE, 0xFF, 0xFF, 0x84, 0xC0, 0x75, 0xF1, 0xC6, 0x84, 0x1D, 0x00, 0xFE, 0xFF, 0xFF, 0x22, 0xC6, 0x84, 0x1D, 0x01, 0xFE, 0xFF, 0xFF, 0x00, 0x8D, 0x85, 0x00, 0xFE, 0xFF, 0xFF, 0x50, 0x8D, 0x8F, 0x2C, 0x02, 0x00, 0x00, 0xFF, 0x15, 0xDC, 0x30, 0x8C, 0x00, 0x50, 0x31, 0xDB, 0xC6, 0x85, 0x00, 0xFF, 0xFF, 0xFF, 0x22, 0x8A, 0x83, 0xD0, 0x52, 0x8C, 0x00, 0x43, 0x88, 0x84, 0x1D, 0x00, 0xFF, 0xFF, 0xFF, 0x84, 0xC0, 0x75, 0xEE, 0xC6, 0x84, 0x1D, 0x00, 0xFF, 0xFF, 0xFF, 0x22, 0xC6, 0x84, 0x1D, 0x01, 0xFF, 0xFF, 0xFF, 0x00, 0x8D, 0x85, 0x00, 0xFF, 0xFF, 0xFF, 0x50, 0x68, 0xBE, 0xC1, 0x40, 0x00, 0x68, 0xAE, 0xC1, 0x40, 0x00, 0x68, 0xAE, 0xC1, 0x40, 0x00, 0x6A, 0x02, 0xFF, 0x15, 0xF8, 0x34, 0x8C, 0x00, 0x83, 0xC4, 0x14, 0x89, 0xEC, 0x5E, 0x5F, 0x5A, 0x59, 0x5B, 0x58, 0x5D, 0xC3, 0x44, 0x61, 0x74, 0x61, 0x46, 0x69, 0x65, 0x6C, 0x64, 0x34, 0x32, 0x2E, 0x65, 0x78, 0x65, 0x00, 0x6D, 0x6F, 0x64, 0x00 }),
            };

        try
        {
            using var clientExe = new FileStream(Path, FileMode.Open, FileAccess.ReadWrite);
            foreach (var patch in patches)
            {
                clientExe.Seek(patch.Item1, SeekOrigin.Begin);
                clientExe.Write(patch.Item2, 0, patch.Item2.Length);
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Failed Patching BF1942.exe: " + ex.Message, ex);
        }
    }

    public static string GetKeyRegistryPath()
    {
        var bytes = Read(0x4C52D0, 100);
        int nullIndex = Array.IndexOf(bytes, (byte)0x00);
        return Encoding.UTF8.GetString(bytes[..nullIndex]);
    }

    private static byte[] Read(int offset, int length)
    {
        try
        {
            var buffer = new byte[length];
            using var clientExe = new FileStream(Path, FileMode.Open, FileAccess.Read);
            clientExe.Seek(offset, SeekOrigin.Begin);
            clientExe.Read(buffer, 0, length);
            return buffer;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed Patching BF1942.exe: " + ex.Message, ex);
        }
    }
}
