using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using DataField42.Website.Data;
using DataField42.Website.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.UserSecrets;
using MySqlConnector;

namespace DataField42.Website.Configuration;

/// <summary>
/// Prepares a database for the website: stores how to reach it, creates it, and applies
/// <c>db/schema.sql</c>.
///
/// This is a command and never runs on startup. The schema is shared with the central server and
/// the servers tracker, so creating it is a decision someone makes, not something the website does
/// because it found the database empty -- which would turn a mistyped connection string into an
/// empty site rather than an error. The script only ever creates what is missing, so running this
/// against a populated database does nothing.
/// </summary>
public static class InitDatabaseCommand
{
    public const string Name = "init-db";

    private const string SchemaFile = "db/schema.sql";

    /// <summary>
    /// Takes an optional connection string. Given one, it is stored in user secrets, so that a
    /// developer never has to find out where settings go. Given none, the configured connection
    /// string is used.
    /// </summary>
    public static async Task<int> RunAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment, string[] args)
    {
        // Already validated: the host would not have been built with a connection string it could
        // not read the server version through.
        string connectionString = configuration.GetDatabaseConnectionString();

        string schemaPath = Path.Combine(environment.ContentRootPath, SchemaFile);
        if (!File.Exists(schemaPath))
        {
            Console.Error.WriteLine($"'{SchemaFile}' is missing from {environment.ContentRootPath}.");
            return 1;
        }

        var settings = new MySqlConnectionStringBuilder(connectionString);

        if (string.IsNullOrWhiteSpace(settings.Database))
        {
            Console.Error.WriteLine("The connection string names no database. Add 'database=DataField42'.");
            return 1;
        }

        try
        {
            await CreateDatabaseAsync(settings);

            if (args.Length > 0)
                SaveSettings(connectionString);

            await ApplySchemaAsync(connectionString, schemaPath);
            await CreateAdminUserTableAsync(services);
        }
        catch (Exception ex) when (DatabaseFailure.IsUnavailable(ex))
        {
            Console.Error.WriteLine($"Could not prepare the database: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Database '{settings.Database}' is ready.");
        return 0;
    }

    /// <summary>
    /// Creates the database, which has to happen on a connection that does not select one.
    /// </summary>
    private static async Task CreateDatabaseAsync(MySqlConnectionStringBuilder settings)
    {
        string databaseName = settings.Database;
        var serverSettings = new MySqlConnectionStringBuilder(settings.ConnectionString) { Database = "" };

        await using var connection = new MySqlConnection(serverSettings.ConnectionString);
        await connection.OpenAsync();

        Console.WriteLine($"Connected to {connection.ServerVersion}.");

        // The name comes from a person, not a request, but it is still quoted rather than
        // concatenated raw -- a database name is an identifier and cannot be a parameter.
        string quotedName = $"`{databaseName.Replace("`", "``")}`";

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS {quotedName} CHARACTER SET utf8mb4";
        await command.ExecuteNonQueryAsync();

        Console.WriteLine($"Database '{databaseName}' exists.");
    }

    /// <summary>
    /// Writes the connection string to this project's user secrets, which is where something
    /// carrying a password belongs: outside the repository, so nobody edits a tracked file to run
    /// the site.
    /// </summary>
    private static void SaveSettings(string connectionString)
    {
        string? secretsId = typeof(InitDatabaseCommand).Assembly
            .GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId;

        if (secretsId is null)
        {
            Console.Error.WriteLine("This build has no user secrets id, so the connection string was not saved.");
            return;
        }

        string path = PathHelper.GetSecretsPathFromSecretsId(secretsId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Merged rather than overwritten, so settings already there survive.
        var secrets = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? []
            : [];

        secrets[DatabaseServiceExtensions.ConnectionStringKey] = connectionString;

        File.WriteAllText(path, secrets.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Saved the connection string to {path}.");
    }

    private static async Task ApplySchemaAsync(string connectionString, string schemaPath)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = await File.ReadAllTextAsync(schemaPath);
        await command.ExecuteNonQueryAsync();

        Console.WriteLine($"Applied {SchemaFile}.");
    }

    /// <summary>
    /// The tables the application owns are created the same way they are on startup, so a database
    /// prepared by this command needs nothing further.
    /// </summary>
    private static async Task CreateAdminUserTableAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataField42DbContext>();
        await AdminSchema.EnsureCreatedAsync(context);

        Console.WriteLine("Admin tables exist.");
    }
}
