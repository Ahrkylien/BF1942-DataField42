using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DataField42.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableObject _currentPageViewModel;

    public MainWindowViewModel()
    {
        try
        {
#if DEBUG
            CommandLineArguments.Parse(new[] { "", "map", "SOFTWARE\\Electronic Arts\\EA GAMES\\Battlefield 1942\\ergc", "1.1.1.1:14567", "", "bf1942/levels/matrix/", "bf1942" });
#else
            CommandLineArguments.Parse(Environment.GetCommandLineArgs());
#endif
        }
        catch (Exception e)
        {
            // TODO: add message 
            //PostError($"Can't parse command line arguments: {e.Message}");
        }

        if (CommandLineArguments.Identifier == CommandLineArgumentIdentifier.DownloadAndJoinServer)
        {
            CurrentPageViewModel = new SyncMenuViewModel();
        }
        else
        {
            CurrentPageViewModel = new ServerListViewModel();
        }
        //CurrentPageViewModel = new SettingsViewModel();
    }
}
