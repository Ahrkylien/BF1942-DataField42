using CommunityToolkit.Mvvm.ComponentModel;
using DataField42.Interfaces;
using DF.Settings;

namespace DataField42.ViewModels;
public partial class SettingsViewModel : ObservableObject, IPageViewModel
{
    public string Title => "Settings";

    private readonly ISettingsSaver _settingsSaver;

    public IEnumerable<ISetting> Settings { get; }

    public SettingsViewModel(IEnumerable<ISetting> settings, ISettingsSaver settingsSaver)
    {
        Settings = settings;

        _settingsSaver = settingsSaver;

        // Hacky way to save the settings:
        // It would be cleaner to create a wrapping VM for each setting to avoid saving the settings if they are edited outside of SettingsViewModel.
        foreach (var setting in Settings)
            setting.PropertyChanged += Setting_PropertyChanged;
    }

    private void Setting_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _settingsSaver.Save();
    }
}