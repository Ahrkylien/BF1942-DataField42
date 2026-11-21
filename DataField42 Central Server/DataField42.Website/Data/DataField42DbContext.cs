using DataField42.Website.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataField42.Website.Data;

/// <summary>
/// Maps onto the central server's existing MariaDB schema. Table and column names are configured
/// explicitly rather than by convention, because the schema predates this application and is
/// shared with the Python central server and the servers tracker -- nothing here may rename a
/// column, and there are deliberately no migrations for the tables those tools also write.
/// </summary>
public class DataField42DbContext(DbContextOptions<DataField42DbContext> options) : DbContext(options)
{
    public DbSet<Mod> Mods => Set<Mod>();

    public DbSet<Map> Maps => Set<Map>();

    public DbSet<Rfa> Rfas => Set<Rfa>();

    public DbSet<Author> Authors => Set<Author>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<TagLink> TagLinks => Set<TagLink>();

    public DbSet<Server> Servers => Set<Server>();

    public DbSet<Round> Rounds => Set<Round>();

    public DbSet<PlayerNumber> PlayerNumbers => Set<PlayerNumber>();

    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    public DbSet<AdminJobRun> AdminJobRuns => Set<AdminJobRun>();

    public DbSet<MapNotInDatabase> MapsNotInDatabase => Set<MapNotInDatabase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureMod(modelBuilder);
        ConfigureMap(modelBuilder);
        ConfigureRfa(modelBuilder);
        ConfigureAuthorAndTags(modelBuilder);
        ConfigureServerAndRounds(modelBuilder);
        ConfigureRequestLog(modelBuilder);
        ConfigureAdminUser(modelBuilder);
        ConfigureAdminJobRun(modelBuilder);
        ConfigureViews(modelBuilder);
    }

    private static void ConfigureMod(ModelBuilder modelBuilder)
    {
        var mod = modelBuilder.Entity<Mod>();
        mod.ToTable("Mod");
        mod.HasKey(m => m.Id);
        mod.Property(m => m.Id).HasColumnName("ID");
        mod.Property(m => m.FolderName).HasColumnName("folderName").HasMaxLength(255);
        mod.Property(m => m.FullName).HasColumnName("fullName").HasMaxLength(255);
        mod.Property(m => m.Version).HasColumnName("version").HasMaxLength(255);
        mod.Property(m => m.LastUpdate).HasColumnName("lastUpdate");
        mod.Property(m => m.AuthorId).HasColumnName("authorID");
        mod.Property(m => m.ModDependencies).HasColumnName("modDependencies").HasMaxLength(8000);
        mod.HasOne(m => m.Author).WithMany().HasForeignKey(m => m.AuthorId).OnDelete(DeleteBehavior.NoAction);
        mod.HasIndex(m => m.FolderName);
    }

    private static void ConfigureMap(ModelBuilder modelBuilder)
    {
        var map = modelBuilder.Entity<Map>();
        map.ToTable("Map");
        map.HasKey(m => m.Id);
        map.Property(m => m.Id).HasColumnName("ID");
        map.Property(m => m.Name).HasColumnName("name").HasMaxLength(255);
        map.Property(m => m.NameRaw).HasColumnName("nameRaw").HasMaxLength(255);
        map.Property(m => m.AuthorId).HasColumnName("authorID");
        map.Property(m => m.Folder).HasColumnName("folder").HasMaxLength(255);
        map.Property(m => m.Readme).HasColumnName("readme").HasMaxLength(255);
        map.Property(m => m.Images).HasColumnName("images").HasMaxLength(4000);
        map.Property(m => m.GpmConquest).HasColumnName("gpm_cq");
        map.Property(m => m.GpmCaptureTheFlag).HasColumnName("gpm_ctf");
        map.Property(m => m.GpmTeamDeathmatch).HasColumnName("gpm_tdm");
        map.Property(m => m.GpmCoop).HasColumnName("gpm_coop");
        map.Property(m => m.GpmObjectiveMode).HasColumnName("gpm_objectivemode");
        map.Property(m => m.ModId).HasColumnName("modID");
        map.Property(m => m.ControlPointsInfo).HasColumnName("controlPointsInfo").HasMaxLength(4000);
        map.Property(m => m.ObjectSpawnsInfo).HasColumnName("objectSpawnsInfo").HasColumnType("longtext");
        map.Property(m => m.GameMapId).HasColumnName("gameMapId").HasMaxLength(40);
        map.Property(m => m.MultiplayerBriefingObjectives).HasColumnName("multiplayerBriefingObjectives").HasColumnType("text");
        map.Property(m => m.DateOldestRfa).HasColumnName("dateOldestRFA");
        map.Property(m => m.DateNewestRfa).HasColumnName("dateNewestRFA");
        map.Ignore(m => m.HasConquest);
        map.Ignore(m => m.HasCaptureTheFlag);
        map.Ignore(m => m.HasTeamDeathmatch);
        map.Ignore(m => m.HasCoop);
        map.Ignore(m => m.HasObjectiveMode);
        map.HasOne(m => m.Mod).WithMany(m => m.Maps).HasForeignKey(m => m.ModId).OnDelete(DeleteBehavior.NoAction);
        map.HasOne(m => m.Author).WithMany(a => a.Maps).HasForeignKey(m => m.AuthorId).OnDelete(DeleteBehavior.NoAction);
        map.HasIndex(m => m.ModId);
        map.HasIndex(m => m.NameRaw);
    }

    private static void ConfigureRfa(ModelBuilder modelBuilder)
    {
        var rfa = modelBuilder.Entity<Rfa>();
        rfa.ToTable("RFA");
        rfa.HasKey(r => r.Id);
        rfa.Property(r => r.Id).HasColumnName("ID");
        rfa.Property(r => r.Path).HasColumnName("path").HasMaxLength(255)
            .UseCollation("utf8_bin");
        rfa.Property(r => r.PatchNumber).HasColumnName("patchNumber");
        rfa.Property(r => r.Note).HasColumnName("note").HasMaxLength(255);
        rfa.Property(r => r.Crc32).HasColumnName("crc32").HasMaxLength(255);
        rfa.Property(r => r.Client).HasColumnName("client");
        rfa.Property(r => r.Recommended).HasColumnName("recommended");
        rfa.Property(r => r.MapId).HasColumnName("mapID");
        rfa.Property(r => r.Date).HasColumnName("date");
        rfa.Property(r => r.BattleCraftLevelPath).HasColumnName("battleCraftLevelPath").HasMaxLength(255);
        rfa.Ignore(r => r.IsClient);
        rfa.Ignore(r => r.IsRecommended);
        rfa.Ignore(r => r.FileName);
        rfa.HasOne(r => r.Map).WithMany(m => m.Rfas).HasForeignKey(r => r.MapId).OnDelete(DeleteBehavior.NoAction);
        rfa.HasIndex(r => r.MapId);
    }

    private static void ConfigureAuthorAndTags(ModelBuilder modelBuilder)
    {
        var author = modelBuilder.Entity<Author>();
        author.ToTable("Author");
        author.HasKey(a => a.Id);
        author.Property(a => a.Id).HasColumnName("ID");
        author.Property(a => a.Name).HasColumnName("name").HasMaxLength(255);

        var tag = modelBuilder.Entity<Tag>();
        tag.ToTable("Tag");
        tag.HasKey(t => t.Id);
        tag.Property(t => t.Id).HasColumnName("ID");
        tag.Property(t => t.Name).HasColumnName("name").HasMaxLength(255);
        tag.Property(t => t.Color).HasColumnName("color").HasMaxLength(255);
        tag.Property(t => t.ColorHover).HasColumnName("colorHover").HasMaxLength(255);

        var tagLink = modelBuilder.Entity<TagLink>();
        tagLink.ToTable("TagLink");
        tagLink.HasKey(t => t.Id);
        tagLink.Property(t => t.Id).HasColumnName("ID");
        tagLink.Property(t => t.MapId).HasColumnName("mapID");
        tagLink.Property(t => t.TagId).HasColumnName("tagID");
        tagLink.HasOne(t => t.Map).WithMany(m => m.TagLinks).HasForeignKey(t => t.MapId).OnDelete(DeleteBehavior.NoAction);
        tagLink.HasOne(t => t.Tag).WithMany(t => t.TagLinks).HasForeignKey(t => t.TagId).OnDelete(DeleteBehavior.NoAction);
        tagLink.HasIndex(t => t.MapId);
    }

    private static void ConfigureServerAndRounds(ModelBuilder modelBuilder)
    {
        var server = modelBuilder.Entity<Server>();
        server.ToTable("Server");
        server.HasKey(s => s.Id);
        server.Property(s => s.Id).HasColumnName("ID");
        server.Property(s => s.Ip).HasColumnName("IP").HasMaxLength(255);
        server.Property(s => s.GamePort).HasColumnName("gamePort");
        server.Property(s => s.QueryPort).HasColumnName("queryPort");
        server.Property(s => s.Name).HasColumnName("name").HasMaxLength(255);

        var round = modelBuilder.Entity<Round>();
        round.ToTable("Round");
        round.HasKey(r => r.Id);
        round.Property(r => r.Id).HasColumnName("ID");
        round.Property(r => r.ServerIp).HasColumnName("serverIP").HasMaxLength(255);
        round.Property(r => r.ServerGamePort).HasColumnName("serverGamePort");
        round.Property(r => r.ServerId).HasColumnName("serverID");
        round.Property(r => r.StartTime).HasColumnName("startTime");
        round.Property(r => r.EndTime).HasColumnName("endTime");
        round.Property(r => r.MapName).HasColumnName("mapName").HasMaxLength(255);
        round.Property(r => r.ModName).HasColumnName("modName").HasMaxLength(255);
        round.Property(r => r.GameplayMode).HasColumnName("gameplayMode").HasMaxLength(255);
        round.Property(r => r.Team1StartTickets).HasColumnName("team1StartTickets");
        round.Property(r => r.Team2StartTickets).HasColumnName("team2StartTickets");
        round.Property(r => r.GameStartDelay).HasColumnName("gameStartDelay");
        round.Property(r => r.RoundTime).HasColumnName("roundTime");
        round.Property(r => r.Team1EndTickets).HasColumnName("team1EndTickets");
        round.Property(r => r.Team2EndTickets).HasColumnName("team2EndTickets");
        round.Property(r => r.RoundTimeRemaining).HasColumnName("roundTimeRemaining");
        round.Property(r => r.WinningTeamNumber).HasColumnName("winningTeamNumber");
        round.Property(r => r.PlayerStats).HasColumnName("playerStats").HasColumnType("longtext");
        round.Property(r => r.AverageNumberOfPlayers).HasColumnName("averageNumberOfPlayers");
        round.Property(r => r.MaximumNumberOfPlayers).HasColumnName("maximumNumberOfPlayers");
        round.Property(r => r.HasHadPlayers).HasColumnName("hasHadPlayers");
        round.HasOne(r => r.Server).WithMany(s => s.Rounds).HasForeignKey(r => r.ServerId).OnDelete(DeleteBehavior.NoAction);
        round.HasIndex(r => new { r.ServerId, r.StartTime, r.WinningTeamNumber }).HasDatabaseName("idx_server_round");

        var playerNumber = modelBuilder.Entity<PlayerNumber>();
        playerNumber.ToTable("PlayerNumber");
        playerNumber.HasKey(p => p.Id);
        playerNumber.Property(p => p.Id).HasColumnName("ID");
        playerNumber.Property(p => p.Date).HasColumnName("date");
        playerNumber.Property(p => p.RoundId).HasColumnName("roundID");
        playerNumber.Property(p => p.Players).HasColumnName("players");
        playerNumber.Property(p => p.PlayersRaw).HasColumnName("playersRaw");
        playerNumber.HasOne(p => p.Round).WithMany(r => r.PlayerNumbers).HasForeignKey(p => p.RoundId).OnDelete(DeleteBehavior.NoAction);
        playerNumber.HasIndex(p => new { p.Id, p.RoundId }).HasDatabaseName("idx_round");
    }

    private static void ConfigureRequestLog(ModelBuilder modelBuilder)
    {
        var requestLog = modelBuilder.Entity<RequestLog>();
        requestLog.ToTable("RequestLog");
        requestLog.HasKey(r => r.Id);
        requestLog.Property(r => r.Id).HasColumnName("ID");
        requestLog.Property(r => r.Type).HasColumnName("type");
        requestLog.Property(r => r.Date).HasColumnName("date");
        requestLog.Property(r => r.Ip).HasColumnName("ip").HasMaxLength(255);
        requestLog.Property(r => r.Field1).HasColumnName("field1").HasMaxLength(255);
        requestLog.Property(r => r.Field2).HasColumnName("field2").HasMaxLength(255);
        requestLog.Property(r => r.Field3).HasColumnName("field3").HasMaxLength(255);
        requestLog.Property(r => r.Field4).HasColumnName("field4").HasMaxLength(2048);
        requestLog.Property(r => r.Field5).HasColumnName("field5").HasMaxLength(255);
        requestLog.Property(r => r.Field6).HasColumnName("field6").HasMaxLength(255);
    }

    private static void ConfigureAdminUser(ModelBuilder modelBuilder)
    {
        var adminUser = modelBuilder.Entity<AdminUser>();
        adminUser.ToTable("AdminUser");
        adminUser.HasKey(a => a.Id);
        adminUser.Property(a => a.Id).HasColumnName("ID");
        adminUser.Property(a => a.UserName).HasColumnName("userName").HasMaxLength(64).IsRequired();
        adminUser.Property(a => a.PasswordHash).HasColumnName("passwordHash").HasMaxLength(255).IsRequired();
        adminUser.Property(a => a.CreatedUtc).HasColumnName("createdUtc");
        adminUser.Property(a => a.LastLoginUtc).HasColumnName("lastLoginUtc");
        adminUser.Property(a => a.SecurityStamp).HasColumnName("securityStamp");
        adminUser.HasIndex(a => a.UserName).IsUnique();
    }

    private static void ConfigureAdminJobRun(ModelBuilder modelBuilder)
    {
        var run = modelBuilder.Entity<AdminJobRun>();
        run.ToTable("AdminJobRun");
        run.HasKey(r => r.Id);
        run.Property(r => r.Id).HasColumnName("ID");
        run.Property(r => r.JobId).HasColumnName("jobId").HasMaxLength(64).IsRequired();
        run.Property(r => r.State).HasColumnName("state").HasMaxLength(16).IsRequired();
        run.Property(r => r.StartedUtc).HasColumnName("startedUtc");
        run.Property(r => r.FinishedUtc).HasColumnName("finishedUtc");
        run.Property(r => r.Parameters).HasColumnName("parameters").HasMaxLength(255);
        run.Property(r => r.Step).HasColumnName("step").HasMaxLength(255);
        run.Property(r => r.CompletedCount).HasColumnName("completedCount");
        run.Property(r => r.TotalCount).HasColumnName("totalCount");
        run.Property(r => r.Error).HasColumnName("error").HasColumnType("text");
        run.Property(r => r.Log).HasColumnName("log").HasColumnType("longtext").IsRequired();
        run.HasIndex(r => new { r.JobId, r.StartedUtc }).HasDatabaseName("IX_AdminJobRun_job");
    }

    private static void ConfigureViews(ModelBuilder modelBuilder)
    {
        var mapNotInDatabase = modelBuilder.Entity<MapNotInDatabase>();
        mapNotInDatabase.HasNoKey();
        mapNotInDatabase.ToView("MapNotInDB");
        mapNotInDatabase.Property(m => m.MapName).HasColumnName("mapName");
        mapNotInDatabase.Property(m => m.ModName).HasColumnName("modName");
    }
}
