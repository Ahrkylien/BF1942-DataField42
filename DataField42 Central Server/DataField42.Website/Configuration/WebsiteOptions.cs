using System.ComponentModel.DataAnnotations;

namespace DataField42.Website.Configuration;

/// <summary>
/// Everything about this deployment that is not code: where the game files live, which external
/// services to consult, and the directories the admin jobs read and write.
/// </summary>
public sealed class WebsiteOptions
{
    public const string SectionName = "Website";

    /// <summary>
    /// The root of the file database. The mods served to clients live in its <c>mods</c>
    /// subdirectory.
    /// </summary>
    [Required]
    public string FileDataBaseDirectory { get; set; } = string.Empty;

    /// <summary>
    /// The master server's JSON endpoint, which the live server list is read from.
    /// </summary>
    public string MasterServerUrl { get; set; } = "http://master.bf1942.org/json?full";

    /// <summary>
    /// How long a server list fetched from the master server is reused before being fetched
    /// again.
    /// </summary>
    public TimeSpan ServerListCacheDuration { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How long to wait for a game server to answer a status query.
    /// </summary>
    public TimeSpan ServerQueryTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Servers that always report one more player than are really playing, by
    /// <c>IP</c> -- a permanently connected bot or spectator. Keyed by IP, valued by the number of
    /// players to subtract.
    /// </summary>
    public Dictionary<string, int> PlayerCountCorrections { get; set; } = [];

    /// <summary>
    /// The server this deployment's own statistics page reports on.
    /// </summary>
    public int? FeaturedServerId { get; set; }

    /// <summary>
    /// Filesystems to report free space for on the extra page, by the name <c>df</c> lists them
    /// under.
    /// </summary>
    public List<string> MonitoredFilesystems { get; set; } = [];

    /// <summary>
    /// The map activity file the server manager writes, read by the private server page. Leave
    /// empty to hide that page.
    /// </summary>
    public string? PlayerActivitiesFile { get; set; }

    /// <summary>
    /// The scraped-pages file the scraping overview reads.
    /// </summary>
    public string? ScrapedPagesFile { get; set; }

    /// <summary>
    /// The folder comparison file the to-do overview reads.
    /// </summary>
    public string? FolderInfosFile { get; set; }

    /// <summary>
    /// Where the admin jobs keep their working files: the server and client file trees, the
    /// generated rules, and uploaded archives.
    /// </summary>
    [Required]
    public string FileTransferDirectory { get; set; } = string.Empty;

    /// <summary>
    /// The mod the maps page opens on.
    /// </summary>
    public string DefaultMod { get; set; } = "BF1942";

    /// <summary>
    /// How the Update page reaches the clone and the update unit on a server.
    /// </summary>
    public UpdateOptions Update { get; set; } = new();

    public string ModsDirectory => Path.Combine(FileDataBaseDirectory, "mods");

    /// <summary>
    /// Resolves the configured directories against <paramref name="contentRootPath"/>, so a
    /// relative path in configuration means "beside the application" rather than "beside whatever
    /// directory it happened to be started from". An absolute path is left as it is.
    /// </summary>
    public void ResolveDirectories(string contentRootPath)
    {
        if (!string.IsNullOrWhiteSpace(FileDataBaseDirectory))
            FileDataBaseDirectory = Path.GetFullPath(FileDataBaseDirectory, contentRootPath);

        if (!string.IsNullOrWhiteSpace(FileTransferDirectory))
            FileTransferDirectory = Path.GetFullPath(FileTransferDirectory, contentRootPath);
    }
}
