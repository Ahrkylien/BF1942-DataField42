using DataField42.Website.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace DataField42.Website.Startup;

/// <summary>
/// Registers the database, and is the one place that knows what its connection string is called.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// The name of the connection string, under <c>ConnectionStrings</c>.
    /// </summary>
    public const string ConnectionStringName = "DataField42";

    /// <summary>
    /// The connection string as configuration addresses it, for the places that write rather than
    /// read it.
    /// </summary>
    public const string ConnectionStringKey = $"ConnectionStrings:{ConnectionStringName}";

    /// <summary>
    /// Registers the database context, and the version of the server it will talk to.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The connection string is missing or invalid, or the server cannot be reached to read its
    /// version.
    /// </exception>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetDatabaseConnectionString();

        // The version decides which SQL is generated, so it is read from the server itself rather
        // than configured: there is then nothing to keep in step with the database. It is read
        // once, here, because doing it inside the options callback would connect on every request.
        var serverVersion = DetectServerVersion(connectionString);

        services.AddSingleton(serverVersion);
        services.AddDbContext<DataField42DbContext>(options =>
            options.UseMySql(connectionString, serverVersion, mySql => mySql.EnableRetryOnFailure()));

        return services;
    }

    /// <summary>
    /// The configured connection string. A missing value and an empty one are the same mistake:
    /// <c>appsettings.json</c> carries the key empty to document it.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is none.</exception>
    public static string GetDatabaseConnectionString(this IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ConnectionStringName);

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' not found. Set '{ConnectionStringKey}' in configuration, "
                + $"or the environment variable '{ConnectionStringKey.Replace(":", "__")}'.")
            : connectionString;
    }

    /// <summary>
    /// Asks the database server which version it is. The question is put to the server and not to
    /// the named database, which a first run has not created yet.
    /// </summary>
    private static ServerVersion DetectServerVersion(string connectionString)
    {
        MySqlConnectionStringBuilder server;
        try
        {
            server = new MySqlConnectionStringBuilder(connectionString) { Database = "" };
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not valid: {ex.Message}", ex);
        }

        try
        {
            return ServerVersion.AutoDetect(server.ConnectionString);
        }
        catch (MySqlException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the database server at {server.Server}:{server.Port} to read its version: {ex.Message}", ex);
        }
    }
}
