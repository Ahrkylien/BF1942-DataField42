using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace DataField42.ViewModels;

public abstract partial class AbstractServerListViewModel : ObservableObject, IPageViewModel
{
    public virtual string Title => "Server List";

    public ObservableCollection<ServerViewModel> Servers { get; set; } = new();

    [ObservableProperty]
    private ICollectionView _serversCollectionView;

    protected readonly MainWindowViewModel _mainWindowViewModel;
    protected readonly Bf1942ServerLobby _serverLobby;
    protected readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly SemaphoreSlim _refreshSemaphore = new(1);
    private bool _isInitialized = false;

    public AbstractServerListViewModel(
        MainWindowViewModel mainWindowViewModel,
        ILogger logger,
        ILoggerFactory loggerFactory,
        Bf1942ServerLobby serverLobby)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _serverLobby = serverLobby;

        _serversCollectionView = CollectionViewSource.GetDefaultView(Servers);
        _serversCollectionView.SortDescriptions.Add(new SortDescription(nameof(ServerViewModel.SortKey), ListSortDirection.Descending));
    }

    public async Task EnterPage()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_isInitialized)
                return;
            _logger.LogDebug($"{GetType().Name} entering page for the first time — refreshing.");
            await Refresh();
            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    [RelayCommand]
    public async Task Refresh()
    {
        await _refreshSemaphore.WaitAsync();
        try
        {
            _logger.LogDebug("Refreshing server list.");
            try
            {
                await _serverLobby.GetServerListFromHttpApi();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch server list.");
                _mainWindowViewModel.DisplayError($"Can't get server list: {ex.Message}");
            }

            var newServers = new List<ServerViewModel>();
            foreach (var server in _serverLobby.Servers)
            {
                if (!Servers.Any(x => x.Equals(server)))
                {
                    var vm = new ServerViewModel(server, ServerSelectedHandler, _loggerFactory.CreateLogger<ServerViewModel>());
                    vm.NewQuery += RefreshList;
                    newServers.Add(vm);
                }
            }

            _logger.LogDebug($"Adding {newServers.Count} new servers to list.");

            await Application.Current.Dispatcher.Invoke(async () =>
            {
                foreach (var serverVm in newServers)
                    Servers.Add(serverVm);

                await _serverLobby.QueryAllServers();
            });

            OnPropertyChanged(nameof(Servers));
            _logger.LogInformation($"Server list refreshed. Total servers: {Servers.Count}.");
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    private void RefreshList()
    {
        ServersCollectionView.Refresh();
    }

    public virtual Task LeavePage() => Task.CompletedTask;

    protected abstract void ServerSelectedHandler(ServerViewModel serverViewModel);
}
