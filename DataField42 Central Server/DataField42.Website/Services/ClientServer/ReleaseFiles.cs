using System.Text;
using System.Text.RegularExpressions;
using DataField42.Website.Configuration;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Services.ClientServer;

/// <summary>
/// The programs clients and servers update themselves from, and the versions this server hands out.
/// </summary>
/// <remarks>
/// The server script is part of this site, kept in memory: the site and the script it hands out are
/// always from the same commit, and its version is read from the script. The programs of the client
/// are built apart from the site and are files in the data folder, put there when a release is made.
/// </remarks>
public sealed class ReleaseFiles(IOptions<WebsiteOptions> options)
{
    /// <summary>
    /// The version of the client, which an older client is told to update to: the version of this
    /// release, which the client is built with.
    /// </summary>
    public static string ClientVersion { get; } = UpdateManager.Version.ToString();

    /// <summary>
    /// The DataField42 server script, which an older server replaces itself with.
    /// </summary>
    public byte[] ServerScript { get; } = ReadServerScript();

    /// <summary>
    /// The version of that script, which a server compares with its own.
    /// </summary>
    public string ServerVersion => _serverVersion ??= ParseVersion(ServerScript);

    /// <summary>
    /// The version a first-generation client is updated to, from where it can update further.
    /// </summary>
    private const string LegacyClientVersion = "2.0.0.0";

    /// <summary>
    /// The only file of the latest client a client may ask for by name.
    /// </summary>
    public const string LatestClientFileName = "DataField42.exe";

    private string? _serverVersion;

    private string DataDirectory => options.Value.FileDataBaseDirectory;

    public string ClientUpdater => Path.Combine(DataDirectory, "client files", ClientVersion, "updater.exe");

    public string LegacyClientUpdater => Path.Combine(DataDirectory, "client files", LegacyClientVersion, "updater.exe");

    public string LatestClientFile => Path.Combine(DataDirectory, "client files", "latest", LatestClientFileName);

    private static byte[] ReadServerScript()
    {
        using var resource = typeof(ReleaseFiles).Assembly.GetManifestResourceStream("DataField42Server.py")
            ?? throw new InvalidOperationException("The DataField42 server script is not part of this build");
        using var memory = new MemoryStream();
        resource.CopyTo(memory);
        return memory.ToArray();
    }

    private static string ParseVersion(byte[] script) =>
        Regex.Match(Encoding.UTF8.GetString(script), @"dataField42_server_version\s*=\s*Version\(""([0-9.]+)""\)") is { Success: true } match
            ? match.Groups[1].Value
            : throw new InvalidOperationException("The DataField42 server script does not say which version it is");
}
