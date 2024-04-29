using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Interfaces;
using System.Collections.ObjectModel;
using System.Windows;

namespace DataField42.ViewModels;
public partial class ServerListViewModel : ObservableObject, IPageViewModel
{
    public ObservableCollection<ServerViewModel> Servers { get; set; } = new();

    private MainWindowViewModel _mainWindowViewModel;

    public ServerListViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        Task.Run(async () => Initialize());
    }

    private async Task Initialize()
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
                Servers.Add(new ServerViewModel(_mainWindowViewModel, server));
            });
        }


        await Application.Current.Dispatcher.Invoke(async () =>
        {
            await serverLobby.QueryAllServers();
        });


        OnPropertyChanged(nameof(Servers));
    }
}


public partial class ServerViewModel : ObservableObject
{
    public string Name => QueryResult?.HostName ?? string.Empty;
    public string Ip => $"{_bf1942Server.Ip}:{QueryResult?.HostPort.ToString() ?? "xxxxx"}";
    public string Players
    {
        get {
            if (QueryResult != null)
            {
                return $"{QueryResult.NumberOfPlayers}/{QueryResult.MaximumNumberOfPlayers}";
            }
            return ""; 
        }
    }
    public string Map => QueryResult?.MapName ?? string.Empty;
    public string Mod => QueryResult?.Mod ?? string.Empty;

    public Bf1942QueryResult? QueryResult => _bf1942Server.QueryResult;

    private MainWindowViewModel _mainWindowViewModel;
    private readonly Bf1942Server _bf1942Server;
    
    public ServerViewModel(MainWindowViewModel mainWindowViewModel, Bf1942Server bf1942Server)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _bf1942Server = bf1942Server;
        bf1942Server.NewQuery += RefreshData;
    }

    [RelayCommand]
    private void Click()
    {
        if (_bf1942Server.QueryResult == null)
        {
            _mainWindowViewModel.DisplayError("Can't sync with server because its not queried.");
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
            _mainWindowViewModel.GoToSyncMenu(new SyncParameters(_bf1942Server.QueryResult.Mod, _bf1942Server.QueryResult.MapName.Replace(' ', '_'), _bf1942Server.Ip, (int)_bf1942Server.QueryResult.HostPort, keyHash, ""));
        }
        catch (Exception ex)
        {
            _mainWindowViewModel.DisplayError(ex.Message);
        }
    }

    private void RefreshData()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Ip));
        OnPropertyChanged(nameof(Players));
        OnPropertyChanged(nameof(Map));
        OnPropertyChanged(nameof(Mod));
    }
}