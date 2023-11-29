using CommunityToolkit.Mvvm.ComponentModel;
using DataField42.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace DataField42.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private IPageViewModel _currentPageViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessages))]
    private string _messages = string.Empty;

    public bool HasMessages => !string.IsNullOrEmpty(Messages);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessages))]
    private string _errorMessages;

    public bool HasErrorMessages => !string.IsNullOrEmpty(ErrorMessages);


    private Dictionary<Page, IPageViewModel> _pages = new();

    public MainWindowViewModel()
    {
        var successfulCommandLineArguments = true;
        try
        {
#if DEBUG
            //CommandLineArguments.Parse(new[] { "", "map", "SOFTWARE\\Electronic Arts\\EA GAMES\\Battlefield 1942\\ergc", "1.1.1.1:14567", "", "bf1942/levels/matrix/", "bf1942" });
            CommandLineArguments.Parse(Environment.GetCommandLineArgs());
#else
            CommandLineArguments.Parse(Environment.GetCommandLineArgs());
#endif
        }
        catch (Exception e)
        {
            DisplayError($"Can't parse command line arguments: {e.Message}");
            successfulCommandLineArguments = false;
        }

        if (successfulCommandLineArguments && CommandLineArguments.Identifier == CommandLineArgumentIdentifier.DownloadAndJoinServer)
        {
            GoToPage(Page.SyncMenu);
        }
        else
        {
            GoToPage(Page.ServerList);
        }
        //CurrentPageViewModel = new SettingsViewModel();
    }

    public void DisplayMessage(string message)
    {
        if (Messages != "")
            message = $"\n{message}";
        Messages += message;
    }

    public void DisplayError(string message)
    {
        if (ErrorMessages != "")
            message = $"\n{message}";
        ErrorMessages += message;
    }


    [MemberNotNull(nameof(CurrentPageViewModel))]
    public void GoToPage(Page page)
    {
        if (!_pages.ContainsKey(page))
            switch (page)
            {
                case Page.ServerList:
                    _pages[page] = new ServerListViewModel(this);
                    break;
                case Page.SyncMenu:
                    _pages[page] = new SyncMenuViewModel(this);
                    break;
                default: throw new Exception($"There is no linked ViewModel for Page: {page} in {nameof(GoToPage)}");
            }

        CurrentPageViewModel = _pages[page];
    }
}

public enum Page
{
    ServerList,
    SyncMenu,
}