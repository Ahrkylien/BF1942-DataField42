using DataField42.Website.Startup;

namespace DataField42.Website.Configuration;

/// <summary>
/// The commands that prepare a machine instead of serving the site, such as <c>init-db</c>. They
/// run on a machine that may have no configuration yet, so they are told apart from a normal start
/// before anything is built.
/// </summary>
public static class SetupCommands
{
    public static bool IsSetupCommand(string[] args) =>
        args is [InitDatabaseCommand.Name or CreateAdminCommand.Name, ..];

    /// <summary>
    /// <c>init-db</c> is how a machine gets its connection string in the first place, so the one it
    /// is given stands in for the configured one -- which on a new machine does not exist yet.
    /// </summary>
    public static void ApplyArguments(IConfiguration configuration, string[] args)
    {
        if (args is [InitDatabaseCommand.Name, var connectionString, ..])
            configuration[DatabaseServiceExtensions.ConnectionStringKey] = connectionString;
    }

    public static Task<int> RunAsync(WebApplication app, string[] args) => args switch
    {
        [CreateAdminCommand.Name, .. var rest] =>
            CreateAdminCommand.RunAsync(app.Services, rest),
        [InitDatabaseCommand.Name, .. var rest] =>
            InitDatabaseCommand.RunAsync(app.Services, app.Configuration, app.Environment, rest),
        _ => throw new ArgumentException($"'{string.Join(' ', args)}' is not a setup command.", nameof(args)),
    };
}
