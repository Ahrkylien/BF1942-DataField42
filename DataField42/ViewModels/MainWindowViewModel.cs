using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Enums;
using DataField42.Interfaces;
using DataField42.Settings;
using DF.Settings;
using ExhaustiveMatching;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace DataField42.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly Bf1942Client _bf1942Client;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;

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

    public MainWindowViewModel(
        SettingsService settingsService,
        Bf1942Client bf1942Client,
        ILoggerFactory loggerFactory)
    {
        _settingsService = settingsService;
        _bf1942Client = bf1942Client;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MainWindowViewModel>();

        _logger.LogInformation("MainWindowViewModel initializing.");

        var successfulCommandLineArguments = true;
        try
        {
            //CommandLineArguments.Parse(new[] { "", "map", "SOFTWARE\\Electronic Arts\\EA GAMES\\Battlefield 1942\\ergc", "1.1.1.1:14567", "", "bf1942/levels/matrix/", "bf1942" });
            CommandLineArguments.Parse(Environment.GetCommandLineArgs());
            _logger.LogDebug($"Command line arguments parsed: Identifier={CommandLineArguments.Identifier}.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to parse command line arguments.");
            DisplayError($"Can't parse command line arguments: {e.Message}");
            successfulCommandLineArguments = false;
        }

        if (!successfulCommandLineArguments)
        {
            // Error is already displayed. This check is needed to avoid going into a statement below
        }
        else if (CommandLineArguments.Identifier == CommandLineArgumentIdentifier.SyncAndJoinServer)
        {
            _logger.LogInformation($"Launching sync menu from command line: mod={CommandLineArguments.Mod}, map={CommandLineArguments.Map}, ip={CommandLineArguments.Ip}, port={CommandLineArguments.Port}.");
            GoToSyncMenu(new SyncParameters(CommandLineArguments.Mod, CommandLineArguments.Map, CommandLineArguments.Ip, CommandLineArguments.Port, CommandLineArguments.KeyHash, CommandLineArguments.Password));
        }
        else if (CommandLineArguments.Identifier == CommandLineArgumentIdentifier.Install)
        {
            _logger.LogInformation("Running in install mode.");
            try
            {
                if (!_bf1942Client.IsDataField42PatchApplied())
                {
                    _logger.LogInformation("Applying DataField42 patch to BF1942.exe.");
                    _bf1942Client.ApplyDataField42Patch();
                }
                else
                {
                    _logger.LogDebug("DataField42 patch already applied.");
                }
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply DataField42 patch.");
                DisplayError(ex.Message);
            }
        }
        else
        {
            if (!_bf1942Client.IsDataField42PatchApplied())
            {
                _logger.LogWarning("DataField42 patch is not applied to BF1942.exe.");
                WarnDataField42PatchNotApplied = true;
            }
            else
            {
                _logger.LogDebug("DataField42 patch verified as applied.");
            }
        }

        _settingsService.SettingChanged += SettingChanged;
        SettingChanged();

        _ = Task.Run(() => GoToPage(_settingsService.Settings.DashboardMode == DashboardMode.Hidden ? Page.ServerList : Page.Dashboard));
    }

    private void SettingChanged()
    {
        DisplayDashboard = _settingsService.Settings.DashboardMode != DashboardMode.Hidden;
        _logger.LogDebug($"Settings changed — DashboardMode={_settingsService.Settings.DashboardMode}, DisplayDashboard={DisplayDashboard}.");
    }

    public void DisplayMessage(string message)
    {
        _logger.LogInformation($"UI message: {message}");
        message = ">> " + message;
        if (HasMessages)
            message = $"\n{message}";
        Messages += message;
    }

    public void DisplayError(string message)
    {
        _logger.LogError($"UI error: {message}");
        message = ">> " + message;
        if (HasErrorMessages)
            message = $"\n{message}";
        ErrorMessages += message;
    }

    public void GoToSyncMenu(SyncParameters syncParameters)
    {
        _logger.LogDebug($"Opening sync menu for {syncParameters.Ip}:{syncParameters.Port}, mod={syncParameters.Mod}, map={syncParameters.Map}.");
        _pages[Page.SyncMenu] = new SyncMenuViewModel(this, _settingsService, syncParameters, _bf1942Client, _loggerFactory);
        DisplayPopup(_pages[Page.SyncMenu]);
    }

    public void GoToSyncMenu(ServerViewModel serverViewModel)
    {
        if (serverViewModel.QueryResult == null)
        {
            _logger.LogWarning($"GoToSyncMenu called for server {serverViewModel.Ip} but QueryResult is null.");
            DisplayError("Can't sync with server because its not queried.");
            return;
        }

        try
        {
            var keyHash = "-";
            try
            {
                keyHash = Md5.Hash(Registry.ReadKey(_bf1942Client.GetKeyRegistryPath()));
                _logger.LogDebug("Registry key hash computed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read registry key hash — using default.");
            }

            GoToSyncMenu(new SyncParameters(
                serverViewModel.QueryResult.Mod,
                serverViewModel.QueryResult.MapName.Replace(' ', '_'),
                serverViewModel.Ip,
                (int)serverViewModel.QueryResult.HostPort,
                keyHash, ""));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to open sync menu for server {serverViewModel.Ip}.");
            DisplayError(ex.Message);
        }
    }

    public void DisplayPopup(IPageViewModel viewModel)
    {
        _logger.LogDebug($"Displaying popup: {viewModel.Title}.");
        CurrentPopUpViewModel = viewModel;
        ShowPopup = true;
        _ = Task.Run(() => CurrentPopUpViewModel.EnterPage());
    }

    [RelayCommand]
    private async Task ClosePopUp()
    {
        _logger.LogDebug($"Closing popup: {CurrentPopUpViewModel?.Title}.");
        _ = Task.Run(() => CurrentPopUpViewModel?.LeavePage());
        ShowPopup = false;
    }

    [RelayCommand]
    private void ApplyDataField42Patch()
    {
        _logger.LogInformation("Applying DataField42 patch manually.");
        try
        {
            _bf1942Client.ApplyDataField42Patch();
            WarnDataField42PatchNotApplied = false;
            _logger.LogInformation("DataField42 patch applied successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply DataField42 patch.");
            DisplayError(ex.Message);
        }
    }

    [RelayCommand]
    [MemberNotNull(nameof(CurrentPageViewModel))]
    private async Task GoToPage(Page page)
    {
        _logger.LogDebug($"Navigating to page: {page}.");

        if (CurrentPageViewModel != null)
            await CurrentPageViewModel.LeavePage();

        CurrentPageViewModel = GetPageViewModel(page);
        CurrentPage = page;

        _logger.LogInformation($"Navigated to page: {page}.");
        _ = Task.Run(() => CurrentPageViewModel.EnterPage());
    }

    private IPageViewModel GetPageViewModel(Page page)
    {
        if (!_pages.ContainsKey(page))
        {
            _logger.LogDebug($"Creating ViewModel for page: {page}.");
            // Create all View Models on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _pages[page] = page switch
                {
                    Page.Dashboard => new DashboardViewModel(this, _settingsService, _loggerFactory),
                    Page.ServerList => new ServerListViewModel(
                        this,
                        _loggerFactory.CreateLogger<AbstractServerListViewModel>(),
                        _loggerFactory,
                        new Bf1942ServerLobby(_loggerFactory.CreateLogger<Bf1942ServerLobby>())),
                    Page.Info => new InfoViewModel(),
                    Page.Settings => new SettingsViewModel(
                        new SettingsProvider<Settings.Settings>(_settingsService.Settings, new SettingSelector()).Settings,
                        _settingsService,
                        _loggerFactory.CreateLogger<SettingsViewModel>()),
                    Page.SyncMenu => throw new ArgumentException($"The {nameof(IPageViewModel)} for {Page.SyncMenu} should be created before calling {nameof(GetPageViewModel)}."),
                    _ => throw ExhaustiveMatch.Failed(page)
                };
            });
        }

        return _pages[page];
    }
}
