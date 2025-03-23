using System.IO;
using System.Net;
using System.Text;

public class SettingsService
{
    private readonly string _filePath;

    public List<(IPAddress, int, string)> FavoriteServers { get; } = new();

    public SettingsService(string filePath)
    {
        _filePath = filePath;
        ParseFile();
    }
    private void ParseFile()
    {
        if (!File.Exists(_filePath))
            FileHelper.WriteText(_filePath, "");

        string[] lines = Array.Empty<string>();
        try
        {
            lines = File.ReadAllLines(_filePath);
        }
        catch (IOException ex)
        {
            // swallow
        }

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("//")) // comment
                continue;

            try
            {
                var lineParts = line.Split(' ', 4);
                if (lineParts[0] == "favoriteServer")
                {
                    FavoriteServers.Add((IPAddress.Parse(lineParts[1]), int.Parse(lineParts[2]), lineParts[3]));
                }
            }
            catch
            {
                // ignore for now
            }
        }
    }

    public void Save()
    {
        var sb = new StringBuilder();
        foreach ((var ip, var port, var name) in FavoriteServers)
            sb.AppendLine($"favoriteServer {ip} {port} {name}");
        FileHelper.WriteText(_filePath, sb.ToString());
    }
}
