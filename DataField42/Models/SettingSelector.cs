using DF.Settings;
using System.Reflection;

namespace DataField42;

public class SettingSelector : IPropertyInfoSettingSelector
{
    private readonly string[] _namesToSkip = new string[] {
        nameof(Settings.Settings.AutoSyncServers),
        nameof(Settings.Settings.IgnoreSyncRules),
        nameof(Settings.Settings.FavoriteServers),
    };

    public bool TryGetSettingFromProperty(PropertyInfo propertyInfo, out ISetting? setting)
    {
        if (_namesToSkip.Contains(propertyInfo.Name))
        {
            setting = null;
            return true;
        }

        setting = null;
        return false;
    }
}