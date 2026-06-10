using DataField42.Settings;
using DF.Settings;
using System.Reflection;

namespace DataField42;

public class SettingSelector : IPropertyInfoSettingSelector
{
    public bool TryGetSettingFromProperty(object settingsObject, PropertyInfo propertyInfo, out ISetting? setting)
    {
        var settings = (Settings.Settings)settingsObject;

        if (propertyInfo.Name == nameof(Settings.Settings.FavoriteServers))
        {
            setting = null;
            return true;
        }

        if (propertyInfo.PropertyType == typeof(List<FileRule>))
        {
            setting = new FileRuleCollectionSetting(
                "Ignore Sync Rules",
                () => settings.IgnoreSyncRules,
                v => settings.IgnoreSyncRules = v);
            return true;
        }

        setting = null;
        return false;
    }
}
