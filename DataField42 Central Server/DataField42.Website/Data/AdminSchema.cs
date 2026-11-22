using Microsoft.EntityFrameworkCore;

namespace DataField42.Website.Data;

/// <summary>
/// Creates the tables this application owns. Everything else in the schema is shared with the
/// central server and the servers tracker, so there are no migrations: a migration history would
/// claim ownership of tables those tools create and alter. These are created here instead --
/// idempotently, so an existing table and its rows are left untouched.
/// </summary>
public static class AdminSchema
{
    private const string CreateAdminUser = """
        CREATE TABLE IF NOT EXISTS `AdminUser` (
          `ID` int(11) NOT NULL AUTO_INCREMENT,
          `userName` varchar(64) NOT NULL,
          `passwordHash` varchar(255) NOT NULL,
          `createdUtc` datetime NOT NULL,
          `lastLoginUtc` datetime DEFAULT NULL,
          `securityStamp` int(11) NOT NULL DEFAULT 0,
          PRIMARY KEY (`ID`),
          UNIQUE KEY `IX_AdminUser_userName` (`userName`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        """;

    private const string CreateAdminJobRun = """
        CREATE TABLE IF NOT EXISTS `AdminJobRun` (
          `ID` int(11) NOT NULL AUTO_INCREMENT,
          `jobId` varchar(64) NOT NULL,
          `state` varchar(16) NOT NULL,
          `startedUtc` datetime NOT NULL,
          `finishedUtc` datetime DEFAULT NULL,
          `parameters` varchar(255) DEFAULT NULL,
          `step` varchar(255) DEFAULT NULL,
          `completedCount` int(11) NOT NULL DEFAULT 0,
          `totalCount` int(11) NOT NULL DEFAULT 0,
          `error` text DEFAULT NULL,
          `log` longtext NOT NULL,
          PRIMARY KEY (`ID`),
          KEY `IX_AdminJobRun_job` (`jobId`,`startedUtc`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        """;

    public static async Task EnsureCreatedAsync(DataField42DbContext context, CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlRawAsync(CreateAdminUser, cancellationToken);
        await context.Database.ExecuteSqlRawAsync(CreateAdminJobRun, cancellationToken);
    }
}
