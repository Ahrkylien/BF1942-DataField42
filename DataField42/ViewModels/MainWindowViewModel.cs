using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DataField42.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableObject _currentPageViewModel;

    public MainWindowViewModel()
    {
        CurrentPageViewModel = new SettingsViewModel();
        CurrentPageViewModel = new SyncMenuViewModel();
        CurrentPageViewModel = new ServerListViewModel();
    }
}
