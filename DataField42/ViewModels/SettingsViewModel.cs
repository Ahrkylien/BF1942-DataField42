using CommunityToolkit.Mvvm.ComponentModel;
using DataField42.Interfaces;
using DF.Settings;
using Microsoft.Extensions.Logging;

namespace DataField42.ViewModels;

public partial class SettingsViewModel : ObservableObject, IPageViewModel
{
    public string Title => "Settings";

    private readonly ISettingsSaver _settingsSaver;
    private readonly ILogger<SettingsViewModel> _logger;

    public IEnumerable<ISetting> Settings { get; }

    public SettingsViewModel(
        IEnumerable<ISetting> settings,
        ISettingsSaver settingsSaver,
        ILogger<SettingsViewModel> logger)
    {
        Settings = settings;
        _settingsSaver = settingsSaver;
        _logger = logger;

        _logger.LogDebug($"SettingsViewModel initialized with {Settings.Count()} settings.");

        // Hacky way to save the settings:
        // It would be cleaner to create a wrapping VM for each setting to avoid saving the settings if they are edited outside of SettingsViewModel.
        foreach (var setting in Settings)
            setting.PropertyChanged += Setting_PropertyChanged;
    }

    private void Setting_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _logger.LogDebug($"Setting changed: {e.PropertyName}. Saving.");
        _settingsSaver.Save();
    }
}
