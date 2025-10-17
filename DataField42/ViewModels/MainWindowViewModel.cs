using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Interfaces;
using ExhaustiveMatching;
using System.Diagnostics.CodeAnalysis;
using DataField42.Enums;
using System.Windows;
using DataField42.Settings;

namespace DataField42.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    private readonly SettingsService _settingsService = new("DataField42/Settings.ini");

    [ObservableProperty]
    private Page? _currentPage;

    [ObservableProperty]
    private bool _displayDashboard;

    [ObservableProperty]
    private IPageViewModel? _currentPageViewModel;

    [ObservableProperty]
    private IPageViewModel? _currentPopUpViewModel;

    [ObservableProperty]
    private bool _showPopup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessages))]
    [NotifyPropertyChangedFor(nameof(HasMessagesOrErrors))]
    private string _messages = string.Empty;

    public bool HasMessages => !string.IsNullOrEmpty(Messages);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessages))]
    [NotifyPropertyChangedFor(nameof(HasMessagesOrErrors))]
    private string _errorMessages = string.Empty;

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

        if (!successfulCommandLineArguments)
        {
            // Error is already displayed. This check is needed to avoid going into a statement below
        }
        else if (CommandLineArguments.Identifier == CommandLineArgumentIdentifier.SyncAndJoinServer)
        {
            GoToSyncMenu(new SyncParameters(CommandLineArguments.Mod, CommandLineArguments.Map, CommandLineArguments.Ip, CommandLineArguments.Port, CommandLineArguments.KeyHash, CommandLineArguments.Password));
        }
        else if (CommandLineArguments.Identifier == CommandLineArgumentIdentifier.Install)
        {
            try
            {
                Bf1942Client.ApplyDataField42Patch();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                DisplayError(ex.Message);
            }
        }

        _settingsService.Settings.DashboardMode.Changed += DashboardModeChanged;
        DashboardModeChanged(_settingsService.Settings.DashboardMode.Value);

        _ = Task.Run(() => GoToPage(_settingsService.Settings.DashboardMode.Value == DashboardMode.Hidden ? Page.ServerList : Page.Dashboard));
    }

    private void DashboardModeChanged(DashboardMode mode)
    {
        DisplayDashboard = mode != DashboardMode.Hidden;
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

    public void GoToSyncMenu(SyncParameters syncParameters)
    {
        _pages[Page.SyncMenu] = new SyncMenuViewModel(this, _settingsService, syncParameters);
        DisplayPopup(_pages[Page.SyncMenu]);
    }

    public void GoToSyncMenu(ServerViewModel serverViewModel)
    {
        if (serverViewModel.QueryResult == null)
        {
            DisplayError("Can't sync with server because its not queried.");
            return;
        }

        try
        {
            var keyHash = "-";
            try
            {
                keyHash = Md5.Hash(Registry.ReadKey(Bf1942Client.GetKeyRegistryPath()));
            }
            catch
            {
                // do nothing
            }
            GoToSyncMenu(new SyncParameters(serverViewModel.QueryResult.Mod, serverViewModel.QueryResult.MapName.Replace(' ', '_'), serverViewModel.Ip, (int)serverViewModel.QueryResult.HostPort, keyHash, ""));
        }
        catch (Exception ex)
        {
            DisplayError(ex.Message);
        }
    }

    public void DisplayPopup(IPageViewModel viewModel)
    {
        CurrentPopUpViewModel = viewModel;
        ShowPopup = true;
        _ = Task.Run(() => CurrentPopUpViewModel.EnterPage());
    }

    [RelayCommand]
    private async Task ClosePopUp()
    {
        _ = Task.Run(() => CurrentPopUpViewModel?.LeavePage());
        ShowPopup = false;
    }

    [RelayCommand]
    [MemberNotNull(nameof(CurrentPageViewModel))]
    private async Task GoToPage(Page page)
    {
        if (CurrentPageViewModel != null)
            await CurrentPageViewModel.LeavePage();

        CurrentPageViewModel = GetPageViewModel(page);

        CurrentPage = page;

        _ = Task.Run(() => CurrentPageViewModel.EnterPage());
    }

    private IPageViewModel GetPageViewModel(Page page)
    {
        if (!_pages.ContainsKey(page))
        {
            // Create all View Models on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _pages[page] = page switch
                {
                    Page.Dashboard => new DashboardViewModel(this, _settingsService),
                    Page.ServerList => new ServerListViewModel(this),
                    Page.Info => new InfoViewModel(),
                    Page.Settings => new SettingsViewModel(_settingsService),
                    Page.SyncMenu => throw new ArgumentException($"The {nameof(IPageViewModel)} for {Page.SyncMenu} should be created before calling {nameof(GetPageViewModel)}."),
                    _ => throw ExhaustiveMatch.Failed(page)
                };
            });
        }
            
        return _pages[page];
    }

    private void Sync(ServerViewModel serverViewModel)
    {

    }
}
