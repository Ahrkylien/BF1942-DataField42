using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace DataField42.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private IPageViewModel _currentPageViewModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessages))]
    [NotifyPropertyChangedFor(nameof(HasMessagesOrErrors))]
    private string _messages = string.Empty;

    public bool HasMessages => !string.IsNullOrEmpty(Messages);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessages))]
    [NotifyPropertyChangedFor(nameof(HasMessagesOrErrors))]
    private string _errorMessages;

    public bool HasErrorMessages => !string.IsNullOrEmpty(ErrorMessages);

    public bool HasMessagesOrErrors => HasMessages || HasErrorMessages;


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
            GoToPage(Page.Info);
            //GoToPage(Page.ServerList);
        }
    }

    public void DisplayMessage(string message)
    {
        message = ">> " + message;
        if (HasMessages)
            message = $"\n{message}";
        Messages += message;
    }

    public void DisplayError(string message)
    {
        message = ">> " + message;
        if (HasErrorMessages)
            message = $"\n{message}";
        ErrorMessages += message;
    }

    [RelayCommand]
    [MemberNotNull(nameof(CurrentPageViewModel))]
    public void GoToPage(Page page)
    {
        if (!_pages.ContainsKey(page))
            _pages[page] = page switch
            {
                Page.ServerList => new ServerListViewModel(this),
                Page.SyncMenu => new SyncMenuViewModel(this),
                Page.Info => new InfoViewModel(),
                _ => throw new Exception($"There is no linked ViewModel for Page: {page} in {nameof(GoToPage)}"),
            };
        CurrentPageViewModel = _pages[page];
    }
}
