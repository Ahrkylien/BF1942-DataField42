using DataField42.Enums;
using DF.Watchable;
using System.Net;

namespace DataField42.Settings;

public class Settings
{
    public DashboardMode DashboardMode { get; init; }

    public List<(IPAddress IpAddress, int Port, string Name)> FavoriteServers { get; set; }

    public bool AutoJoin { get; set; }

    public List<string> AutoSyncServers { get; set; }

    public List<FileRule> IgnoreSyncRules { get; set; }
}
