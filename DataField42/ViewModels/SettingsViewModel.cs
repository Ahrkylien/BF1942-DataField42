using CommunityToolkit.Mvvm.ComponentModel;
using DataField42.Enums;
using DataField42.Interfaces;
using DataField42.Settings;
using System.Collections.ObjectModel;

namespace DataField42.ViewModels;
public partial class SettingsViewModel : ObservableObject, IPageViewModel
{
    public string Title => "Settings";

    private readonly SettingsService _settingsService;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;

        // Populate enum values for binding
        DashboardModes = new ObservableCollection<DashboardMode>(
            Enum.GetValues(typeof(DashboardMode)) as DashboardMode[]
        );

        // Subscribe to changes in Watchable
        _settingsService.Settings.DashboardMode.Changed += value => OnPropertyChanged(nameof(SelectedDashboardMode));
    }

    // Expose the list of enum values for a ComboBox
    public ObservableCollection<DashboardMode> DashboardModes { get; }

    // Expose the current value for binding
    public DashboardMode SelectedDashboardMode
    {
        get => _settingsService.Settings.DashboardMode.Value;
        set => _settingsService.Settings.DashboardMode.Value = value;
    }
}
