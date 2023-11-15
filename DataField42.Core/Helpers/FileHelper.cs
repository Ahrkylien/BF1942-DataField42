﻿using System.IO;

public static class FileHelper
{
    public static void MoveAndCreateDirectory(string sourcePath, string destinationPath, bool overwrite = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? "");
        File.Move(sourcePath, destinationPath, overwrite);
    }

    public static void SetLastWriteTime(string filePath, ulong lastModifiedTimestamp)
    {
        File.SetLastWriteTime(filePath, DateTimeOffset.FromUnixTimeSeconds((long) lastModifiedTimestamp).UtcDateTime);
    }
}