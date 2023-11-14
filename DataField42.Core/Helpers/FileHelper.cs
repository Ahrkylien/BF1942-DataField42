using System.IO;

public static class FileHelper
{
    public static void MoveAndCreateDirectory(string sourcePath, string destinationPath, bool overwrite = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? "");
        File.Move(sourcePath, destinationPath, overwrite);
    }
}