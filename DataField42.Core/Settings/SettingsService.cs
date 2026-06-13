using DataField42.Enums;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace DataField42.Settings;

public class SettingsService : ISettingsSaver
{
    private readonly string _filePath;

    public Settings Settings { get; }

    public event Action? SettingChanged;

    public SettingsService(string filePath)
    {
        _filePath = filePath;
        Settings = ParseFile(filePath);
    }

    private static Settings ParseFile(string filePath)
    {
        // Optional improvement: use System.Runtime.Serialization.IFormatter

        var halfParsedSettings = ParseFileIntoBasicTypes(filePath);

        var favoriteServers = new List<(IPAddress, int, string)>();

        foreach (var settingString in halfParsedSettings.Application.FavoriteServers)
        {
            var parts = settingString.Split(' ', 3);
            favoriteServers.Add((IPAddress.Parse(parts[0]), int.Parse(parts[1]), parts[2]));
        }

        var ignoreSyncRules = new List<FileRule>();

        foreach (var ruleString in halfParsedSettings.SynchronisationRules.IgnoreSyncRules)
        {
            var parts = ruleString.Split(' ', 4);
            ignoreSyncRules.Add(new FileRule(parts[0], parts[1], parts[2], parts[3]));
        }

        return new Settings(
            dashboardMode: Enum.Parse<DashboardMode>(halfParsedSettings.Application.DashboardMode),
            favoriteServers: favoriteServers,
            autoJoin: halfParsedSettings.Application.AutoJoin,
            autoSyncServers: halfParsedSettings.Application.AutoSyncServers,
            ignoreSyncRules: ignoreSyncRules
        );
    }

    private static IniSettings ParseFileIntoBasicTypes(string filePath)
    {
        if (!File.Exists(filePath))
        {
            var defaultSettings = new Settings(
                dashboardMode: DashboardMode.FourServers,
                favoriteServers: [],
                autoJoin: false,
                autoSyncServers: [],
                ignoreSyncRules: [new FileRule(IgnoreSyncScenario.Always, Bf1942FileType.ModMiscFile, "*", "mod.dll")]
            );
            Save(defaultSettings, filePath);
        }

        var configuration = new ConfigurationBuilder()
            .AddIniFile(filePath)
            .Build();

        var settings = new IniSettings();
        configuration.Bind(settings);

        return settings;
    }

    public void Save()
    {
        Save(Settings, _filePath);
        SettingChanged?.Invoke();
    }

    private static void Save(Settings settings, string filePath)
    {
        var iniFile = new IniFile(filePath);

        iniFile.Add(nameof(IniSettings.Application), nameof(ApplicationSection.DashboardMode), $"{settings.DashboardMode}");

        iniFile.Add(nameof(IniSettings.Application), nameof(ApplicationSection.FavoriteServers), settings.FavoriteServers.Select((x) => $"{x.IpAddress} {x.Port} {x.Name}"));

        iniFile.Add(nameof(IniSettings.Application), nameof(ApplicationSection.AutoJoin), $"{settings.AutoJoin}");

        iniFile.Add(nameof(IniSettings.Application), nameof(ApplicationSection.AutoSyncServers), settings.AutoSyncServers);

        iniFile.Add(nameof(IniSettings.SynchronisationRules), nameof(SynchronisationRulesSection.IgnoreSyncRules), settings.IgnoreSyncRules.Select((x) => x.Serialize()));

        iniFile.Save();
    }
}
