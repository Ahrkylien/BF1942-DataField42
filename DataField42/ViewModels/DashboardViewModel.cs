using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Interfaces;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Windows;

namespace DataField42.ViewModels;
public partial class DashboardViewModel : ObservableObject, IPageViewModel
{
    public string Title => "Dashboard";

    private readonly MainWindowViewModel _mainWindowViewModel;

    private readonly SettingsService _settingsService;

    private readonly SemaphoreSlim _semaphore = new(1);

    private bool _isInitialized = false;

    public ObservableCollection<ServerViewModel> FavoriteServers { get; set; } = new ();

    [ObservableProperty]
    private bool _editModeEnabled;

    [DesignOnly(true)]
    public DashboardViewModel()
    {
        _mainWindowViewModel = new MainWindowViewModel();
        _settingsService = new SettingsService("DataField/Settings.txt");
        FavoriteServers.Add(new ServerViewModel(new Bf1942Server(IPAddress.Parse("1.2.3.4"), 23000), ServerSelectedHandler, "BFServer 1", queryServer: true));
        EditModeEnabled = false;
    }

    public DashboardViewModel(MainWindowViewModel mainWindowViewModel, SettingsService settingsService)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _settingsService = settingsService;
        foreach ((var ip, var port, var name) in _settingsService.FavoriteServers)
            FavoriteServers.Add(new ServerViewModel(new Bf1942Server(ip, port), ServerSelectedHandler, name, queryServer: true));
        EditModeEnabled = false;
    }

    public async Task EnterPage()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_isInitialized)
                return;
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
    /// <returns></returns>
    private async Task Initialize()
    {
        if (FavoriteServers.Count >= 4)
            return;

        var serverLobby = new Bf1942ServerLobby();
        try
        {
            await serverLobby.GetServerListFromHttpApi();
        }
        catch (Exception ex)
        {
            _mainWindowViewModel.DisplayError($"Can't get server list: {ex.Message}");
        }

        foreach (var server in serverLobby.Servers)
        {
            if (!FavoriteServers.Any(x => x.Ip == server.Ip.ToString() && x.QueryPort == server.QueryPort))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FavoriteServers.Add(new ServerViewModel(server, ServerSelectedHandler, "loading...", queryServer: true) { GeneratedItem = true });
                });
                if (FavoriteServers.Count >= 4)
                    break;
            }
        }

        // if the servers list could not be retrived or there are to few, add up to 4:
        for (var i = FavoriteServers.Count; i < 4; i++)
            FavoriteServers.Add(new ServerViewModel(new Bf1942Server(IPAddress.Parse("1.1.1.1"), 23000), ServerSelectedHandler, "Go to edit mode to select server") { GeneratedItem = true });
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        if (EditModeEnabled)
        {
            _settingsService.FavoriteServers.Clear();
            foreach (var serverViewModel in FavoriteServers)
                if (!serverViewModel.GeneratedItem)
                    _settingsService.FavoriteServers.Add((IPAddress.Parse(serverViewModel.Ip), serverViewModel.QueryPort, serverViewModel.Name));
            _settingsService.Save();
        }

        EditModeEnabled = !EditModeEnabled;
    }

    [RelayCommand]
    private async Task Edit(ServerViewModel serverViewModel)
    {
        var newServerViewModel = await GetServerViewModelFromUserInput();
        if (newServerViewModel == null)
            return;

        FavoriteServers[FavoriteServers.IndexOf(serverViewModel)] = newServerViewModel;
    }

    private async Task<ServerViewModel?> GetServerViewModelFromUserInput()
    {
        var serverSelectionViewModel = new ServerSelectionViewModel(_mainWindowViewModel);
        _mainWindowViewModel.DisplayPopup(serverSelectionViewModel);
        return await serverSelectionViewModel.AwaitSelection();
    }

    private void ServerSelectedHandler(ServerViewModel serverViewModel)
    {
        _mainWindowViewModel.GoToSyncMenu(serverViewModel);
    }
}
