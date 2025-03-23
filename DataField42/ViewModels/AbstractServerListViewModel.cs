using CommunityToolkit.Mvvm.ComponentModel;
using DataField42.Interfaces;
using System.Collections.ObjectModel;
using System.Windows;

namespace DataField42.ViewModels;

public abstract partial class AbstractServerListViewModel : ObservableObject, IPageViewModel
{
    public virtual string Title => "Server List";

    public ObservableCollection<ServerViewModel> Servers { get; set; } = new();

    protected readonly MainWindowViewModel _mainWindowViewModel;

    private readonly SemaphoreSlim _semaphore = new(1);

    private bool _isInitialized = false;

    public AbstractServerListViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
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

    public async Task Initialize()
    {
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                Servers.Add(new ServerViewModel(server, ServerSelectedHandler));
            });
        }


        await Application.Current.Dispatcher.Invoke(async () =>
        {
            await serverLobby.QueryAllServers();
        });


        OnPropertyChanged(nameof(Servers));
    }

    public virtual Task LeavePage() => Task.CompletedTask;

    protected abstract void ServerSelectedHandler(ServerViewModel serverViewModel);
}
