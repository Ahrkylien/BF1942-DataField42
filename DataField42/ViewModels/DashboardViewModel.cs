using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Enums;
using DataField42.Interfaces;
using DataField42.Settings;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net;
using System.Windows;

namespace DataField42.ViewModels;

public partial class DashboardViewModel : ObservableObject, IPageViewModel
{
    public string Title => "Dashboard";

    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly SettingsService _settingsService;
    private readonly Bf1942ServerLobby _serverLobby;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly SemaphoreSlim _semaphore = new(1);
    private bool _isInitialized = false;

    public ObservableCollection<ServerViewModel> FavoriteServers { get; } = new();
    public ServerViewModel? FirstFavoriteServers => FavoriteServers.FirstOrDefault();

    [ObservableProperty]
    private bool _editModeEnabled;

    [ObservableProperty]
    private bool _displayOneServer;

    [DesignOnly(true)]
    public DashboardViewModel()
    {
        _mainWindowViewModel = null!;
        _settingsService = null!;
        _serverLobby = null!;
        _logger = null!;
        _loggerFactory = null!;
        FavoriteServers.Add(new ServerViewModel(new Bf1942Server(IPAddress.Parse("1.2.3.4"), 23000), ServerSelectedHandler, null!, null!, "BFServer 1"));
        EditModeEnabled = false;
    }

    public DashboardViewModel(
        MainWindowViewModel mainWindowViewModel,
        SettingsService settingsService,
        Bf1942ServerLobby serverLobby,
        ILoggerFactory loggerFactory)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _settingsService = settingsService;
        _serverLobby = serverLobby;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DashboardViewModel>();

        _logger.LogDebug("DashboardViewModel initializing.");

        foreach ((var ip, var port, var name) in _settingsService.Settings.FavoriteServers)
            FavoriteServers.Add(new ServerViewModel(new Bf1942Server(ip, port), ServerSelectedHandler, _mainWindowViewModel.GoToSyncMenu, _loggerFactory.CreateLogger<ServerViewModel>(), name, queryServer: true));

        EditModeEnabled = false;
        _settingsService.SettingChanged += SettingChanged;
        SettingChanged();
        FavoriteServers.CollectionChanged += FavoriteServers_CollectionChanged;

        _logger.LogDebug($"DashboardViewModel initialized with {FavoriteServers.Count} favorite servers.");
    }

    private void FavoriteServers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(FirstFavoriteServers));
    }

    private void SettingChanged()
    {
        DisplayOneServer = _settingsService.Settings.DashboardMode == DashboardMode.SingleServer;
        _logger.LogDebug($"Settings changed — DashboardMode={_settingsService.Settings.DashboardMode}, DisplayOneServer={DisplayOneServer}.");
    }

    public async Task EnterPage()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_isInitialized)
                return;
            _logger.LogDebug("DashboardViewModel entering page for first time — initializing.");
            await Initialize();
            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// This Task will fill the dashboard with generated data.
    /// For now this entails that if less than 4 favorite servers are selected it will look up the most populated servers and fill up to 4 servers.
    /// </summary>
    private async Task Initialize()
    {
        _logger.LogDebug($"Dashboard initialize: {FavoriteServers.Count} favorite servers, target=4.");

        if (FavoriteServers.Count >= 4)
        {
            _logger.LogDebug("Already have 4+ favorite servers, skipping auto-fill.");
            return;
        }

        try
        {
            await _serverLobby.GetServerListFromHttpApi();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch server list for dashboard fill.");
            _mainWindowViewModel.DisplayError($"Can't get server list: {ex.Message}");
        }

        foreach (var server in _serverLobby.Servers)
        {
            if (!FavoriteServers.Any(x => x.Ip == server.Ip.ToString() && x.QueryPort == server.QueryPort))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FavoriteServers.Add(new ServerViewModel(server, ServerSelectedHandler, _mainWindowViewModel.GoToSyncMenu, _loggerFactory.CreateLogger<ServerViewModel>(), "loading...", queryServer: true) { GeneratedItem = true });
                });
                _logger.LogDebug($"Auto-added generated server {server.Ip}:{server.QueryPort} to dashboard.");
                if (FavoriteServers.Count >= 4)
                    break;
            }
        }

        // if the servers list could not be retrived or there are to few, add up to 4:
        for (var i = FavoriteServers.Count; i < 4; i++)
        {
            FavoriteServers.Add(new ServerViewModel(new Bf1942Server(IPAddress.Parse("1.1.1.1"), 23000), ServerSelectedHandler, _mainWindowViewModel.GoToSyncMenu, _loggerFactory.CreateLogger<ServerViewModel>(), "Go to edit mode to select server") { GeneratedItem = true });
            _logger.LogDebug($"Added placeholder server slot {i + 1}.");
        }
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        if (EditModeEnabled)
        {
            _logger.LogDebug("Saving favorite servers on edit mode exit.");
            _settingsService.Settings.FavoriteServers.Clear();
            foreach (var serverViewModel in FavoriteServers)
                if (!serverViewModel.GeneratedItem)
                    _settingsService.Settings.FavoriteServers.Add((IPAddress.Parse(serverViewModel.Ip), serverViewModel.QueryPort, serverViewModel.Name));
            _settingsService.Save();
        }

        EditModeEnabled = !EditModeEnabled;
        _logger.LogDebug($"Edit mode toggled: {EditModeEnabled}.");
    }

    [RelayCommand]
    private async Task Edit(ServerViewModel serverViewModel)
    {
        _logger.LogDebug($"Editing favorite server slot for {serverViewModel.Ip}.");
        var newServerViewModel = await GetServerViewModelFromUserInput();
        if (newServerViewModel == null)
        {
            _logger.LogDebug("Server selection cancelled.");
            return;
        }

        _logger.LogDebug($"Replacing server with selection: {newServerViewModel.Ip}:{newServerViewModel.QueryPort}.");
        FavoriteServers[FavoriteServers.IndexOf(serverViewModel)] = (ServerViewModel)newServerViewModel;
    }

    private async Task<ServerViewModel?> GetServerViewModelFromUserInput()
    {
        var vm = new ServerSelectionViewModel(
            _mainWindowViewModel,
            _loggerFactory.CreateLogger<AbstractServerListViewModel>(),
            _loggerFactory,
            _serverLobby);
        _mainWindowViewModel.DisplayPopup(vm);
        return await vm.AwaitSelection();
    }

    private void ServerSelectedHandler(ServerViewModel serverViewModel)
    {
        _logger.LogDebug($"Favorite server selected: {serverViewModel.Ip}:{serverViewModel.QueryPort}.");
        _mainWindowViewModel.DisplayServerInfo(serverViewModel);
    }

    public Task LeavePage() => Task.CompletedTask;
}
