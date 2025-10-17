using DataField42.Enums;
using DF.Watchable;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Net;

namespace DataField42.Settings;

public class SettingsService
{
    private readonly string _filePath;

    public Settings Settings { get; }

    public SettingsService(string filePath)
    {
        _filePath = filePath;
        Settings = ParseFile(filePath);

        Settings.DashboardMode.PropertyChanged += DashboardMode_PropertyChanged;
    }

    private void DashboardMode_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Save();
    }

    private static Settings ParseFile(string filePath)
    {
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

        return new Settings()
        {
            DashboardMode = new Watchable<DashboardMode>(Enum.Parse<DashboardMode>(halfParsedSettings.Application.DashboardMode)),
            FavoriteServers = favoriteServers,
            AutoJoin = halfParsedSettings.Application.AutoJoin,
            AutoSyncServers = halfParsedSettings.Application.AutoSyncServers,
            IgnoreSyncRules = ignoreSyncRules
        };
    }

    private static IniSettings ParseFileIntoBasicTypes(string filePath)
    {
        if (!File.Exists(filePath))
            FileHelper.WriteText(filePath, "[SynchronisationRules]\nIgnoreSyncRules: 0 = Always ModMiscFile * mod.dll");

        var configuration = new ConfigurationBuilder()
            .AddIniFile(filePath)
            .Build();

        var settings = new IniSettings();
        configuration.Bind(settings);

        return settings;
    }

    public void Save()
    {
        var iniFile = new IniFile(_filePath);

        iniFile.Add(nameof(IniSettings.Application), nameof(ApplicationSection.DashboardMode), $"{Settings.DashboardMode.Value}");

        iniFile.Add(nameof(IniSettings.Application), nameof(ApplicationSection.FavoriteServers), Settings.FavoriteServers.Select((x) => $"{x.IpAddress} {x.Port} {x.Name}"));

        iniFile.Add(nameof(IniSettings.Application), nameof(ApplicationSection.AutoJoin), $"{Settings.AutoJoin}");

        iniFile.Add(nameof(IniSettings.Application), nameof(ApplicationSection.AutoSyncServers), Settings.AutoSyncServers);

        iniFile.Add(nameof(IniSettings.SynchronisationRules), nameof(SynchronisationRulesSection.IgnoreSyncRules), Settings.IgnoreSyncRules.Select((x) => x.Serialize()));

        iniFile.Save();
    }
}
