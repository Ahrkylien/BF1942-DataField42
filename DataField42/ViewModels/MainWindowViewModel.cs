using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Enums;
using DataField42.Interfaces;
using DataField42.Settings;
using DF.Settings;
using ExhaustiveMatching;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace DataField42.ViewModels;
public partial class MainWindowViewModel : ObservableObject
{
    private readonly SettingsService _settingsService = new("DataField42/Settings.ini");

    private readonly Bf1942Client _bf1942Client = new("BF1942.exe");

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

    public bool HasMessagesOrErrors => HasMessages || HasErrorMessages || WarnDataField42PatchNotApplied;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessagesOrErrors))]
    private bool _warnDataField42PatchNotApplied;

    private readonly Dictionary<Page, IPageViewModel> _pages = [];

    public MainWindowViewModel()
    {
        var successfulCommandLineArguments = true;
        try
        {
            //CommandLineArguments.Parse(new[] { "", "map", "SOFTWARE\\Electronic Arts\\EA GAMES\\Battlefield 1942\\ergc", "1.1.1.1:14567", "", "bf1942/levels/matrix/", "bf1942" });
            CommandLineArguments.Parse(Environment.GetCommandLineArgs());
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
                if (!_bf1942Client.IsDataField42PatchApplied())
                    _bf1942Client.ApplyDataField42Patch();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                DisplayError(ex.Message);
            }
        }
        else
        {
            if (!_bf1942Client.IsDataField42PatchApplied())
                WarnDataField42PatchNotApplied = true;
        }

        _settingsService.SettingChanged += SettingChanged;
        SettingChanged();

        _ = Task.Run(() => GoToPage(_settingsService.Settings.DashboardMode == DashboardMode.Hidden ? Page.ServerList : Page.Dashboard));
    }

    private void SettingChanged()
    {
        DisplayDashboard = _settingsService.Settings.DashboardMode != DashboardMode.Hidden;
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
        _pages[Page.SyncMenu] = new SyncMenuViewModel(this, _settingsService, syncParameters, _bf1942Client);
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
                keyHash = Md5.Hash(Registry.ReadKey(_bf1942Client.GetKeyRegistryPath()));
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
    private void ApplyDataField42Patch()
    {
        try
        {
            _bf1942Client.ApplyDataField42Patch();
            WarnDataField42PatchNotApplied = false;
        }
        catch(Exception ex)
        {
            DisplayError(ex.Message);
        }
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
                    Page.Settings => new SettingsViewModel(new SettingsProvider<Settings.Settings>(_settingsService.Settings, new SettingSelector()).Settings, _settingsService),
                    Page.SyncMenu => throw new ArgumentException($"The {nameof(IPageViewModel)} for {Page.SyncMenu} should be created before calling {nameof(GetPageViewModel)}."),
                    _ => throw ExhaustiveMatch.Failed(page)
                };
            });
        }
            
        return _pages[page];
    }
}
