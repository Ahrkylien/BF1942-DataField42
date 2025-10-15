using CommunityToolkit.Mvvm.ComponentModel;
using DataField42.Enums;
using DataField42.Interfaces;
using System.Collections.ObjectModel;

namespace DataField42.ViewModels;
public partial class SettingsViewModel : ObservableObject, IPageViewModel
{
    public string Title => "Settings";

    private readonly Settings _settings;

    public SettingsViewModel(Settings settings)
    {
        _settings = settings;

        // Populate enum values for binding
        DashboardModes = new ObservableCollection<DashboardMode>(
            Enum.GetValues(typeof(DashboardMode)) as DashboardMode[]
        );

        // Subscribe to changes in Watchable
        _settings.DashboardMode.Changed += value => OnPropertyChanged(nameof(SelectedDashboardMode));
    }

    // Expose the list of enum values for a ComboBox
    public ObservableCollection<DashboardMode> DashboardModes { get; }

    // Expose the current value for binding
    public DashboardMode SelectedDashboardMode
    {
        get => _settings.DashboardMode.Value;
        set => _settings.DashboardMode.Value = value;
    }
}
