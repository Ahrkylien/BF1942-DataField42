namespace DataField42.Website.Configuration;

/// <summary>
/// The folders the admin panel's Update page works with. All four are set on a server installed with
/// <c>setup-server.sh</c>; left empty, as on a development machine, the page says updating is not
/// available.
/// </summary>
public sealed class UpdateOptions
{
    /// <summary>
    /// The git clone updates are fetched into and built from.
    /// </summary>
    public string? SourceDirectory { get; set; }

    /// <summary>
    /// Where a build is made before it replaces anything, so a build that fails leaves the live site
    /// alone.
    /// </summary>
    public string? BuildDirectory { get; set; }

    /// <summary>
    /// The folder the site runs from, which a successful build is copied over.
    /// </summary>
    public string? TargetDirectory { get; set; }

    /// <summary>
    /// Where the update keeps its status, its log and a record of the version it deployed.
    /// </summary>
    public string? StateDirectory { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SourceDirectory)
        && !string.IsNullOrWhiteSpace(BuildDirectory)
        && !string.IsNullOrWhiteSpace(TargetDirectory)
        && !string.IsNullOrWhiteSpace(StateDirectory);
}
