namespace DataField42.Website.Services.Admin.Update;

/// <summary>
/// Where the last update stands.
/// </summary>
public enum UpdateState
{
    /// <summary>
    /// No update has run on this server yet.
    /// </summary>
    Idle,

    Running,

    Succeeded,

    /// <summary>
    /// The update stopped with an error. Unless it got as far as deploying, the site is still the
    /// version it was.
    /// </summary>
    Failed,

    /// <summary>
    /// The update was recorded as running, but nothing is running it any more.
    /// </summary>
    Interrupted,
}
