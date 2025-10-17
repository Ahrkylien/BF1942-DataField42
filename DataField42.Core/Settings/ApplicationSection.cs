namespace DataField42.Settings;

public class ApplicationSection
{
    public string DashboardMode { get; set; } = Enums.DashboardMode.Hidden.ToString();

    public List<string> FavoriteServers { get; set; } = new ();

    public List<string> AutoSyncServers { get; set; } = new ();

    public bool AutoJoin { get; set; } = false;
}
