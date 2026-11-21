namespace DataField42.Website.Startup;

/// <summary>
/// Adds the per-developer settings file to configuration.
/// </summary>
public static class LocalSettingsExtensions
{
    /// <summary>
    /// In Development, reads <c>appsettings.Local.json</c> if there is one: the settings that
    /// differ between developers but are not secret, such as where the game files are kept. It is
    /// untracked, so nobody edits a committed file to run the site.
    /// </summary>
    public static ConfigurationManager AddLocalSettings(this ConfigurationManager configuration, IHostEnvironment environment, string[] args)
    {
        if (!environment.IsDevelopment())
            return configuration;

        configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

        // Added again so that the environment and the command line still win over the local file,
        // which is what anyone overriding a setting for one run expects.
        configuration.AddEnvironmentVariables();
        configuration.AddCommandLine(args);

        return configuration;
    }
}
