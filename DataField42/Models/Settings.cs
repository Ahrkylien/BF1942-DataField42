using DataField42.Enums;
using DF.Watchable;

namespace DataField42;
public class Settings
{
    public Watchable<DashboardMode> DashboardMode { get; } = new Watchable<DashboardMode>(Enums.DashboardMode.Hidden);
}
