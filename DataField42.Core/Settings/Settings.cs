using DataField42.Enums;
using DF.Watchable;
using System.Net;

namespace DataField42.Settings;

public class Settings(DashboardMode dashboardMode, List<(IPAddress IpAddress, int Port, string Name)> favoriteServers, bool autoJoin, List<string> autoSyncServers, List<FileRule> ignoreSyncRules)
{
    public DashboardMode DashboardMode { get; init; } = dashboardMode;

    public List<(IPAddress IpAddress, int Port, string Name)> FavoriteServers { get; set; } = favoriteServers;

    public bool AutoJoin { get; set; } = autoJoin;

    public List<string> AutoSyncServers { get; set; } = autoSyncServers;

    public List<FileRule> IgnoreSyncRules { get; set; } = ignoreSyncRules;
}
