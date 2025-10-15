using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Interfaces;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.DirectoryServices;
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

    protected readonly Bf1942ServerLobby _serverLobby = new();

    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly SemaphoreSlim _refreshSemaphore = new(1);

    private bool _isInitialized = false;

    public AbstractServerListViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;

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
            try
            {
                await _serverLobby.GetServerListFromHttpApi();
            }
            catch (Exception ex)
            {
                _mainWindowViewModel.DisplayError($"Can't get server list: {ex.Message}");
            }

            var newServers = new List<ServerViewModel>();

            foreach (var server in _serverLobby.Servers)
            {
                if (!Servers.Any(x => x.Equals(server)))
                {
                    var vm = new ServerViewModel(server, ServerSelectedHandler);
                    vm.NewQuery += RefreshList;
                    newServers.Add(vm);
                }
            }

            await Application.Current.Dispatcher.Invoke(async () =>
            {
                foreach (var serverVm in newServers)
                    Servers.Add(serverVm);

                await _serverLobby.QueryAllServers();
            });

            OnPropertyChanged(nameof(Servers));
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
