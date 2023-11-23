using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace DataField42.ViewModels;
public partial class ServerListViewModel : ObservableObject
{
    //[ObservableProperty]
    //private List<Server> _servers = new();
    public ObservableCollection<Server> Servers { get; set; } = new();

    public ServerListViewModel()
    {
        //Task.Run(async () => Initialize());
        
    }

    [RelayCommand]
    public async Task Initialize()
    {
        var serverLobby = new Bf1942ServerLobby();
        await serverLobby.GetFromMasterApi();
        foreach (var server in serverLobby.Servers)
            Servers.Add(new Server(server));
    }
}


public class Server
{
    public string Name { get; set; }
    public string Ip { get; set; }

    private readonly Bf1942Server _bf1942Server;
    public Server(Bf1942Server bf1942Server)
    {
        _bf1942Server = bf1942Server;
        Name = _bf1942Server.QueryResult?.HostName ?? "Not Queried";
        Ip = _bf1942Server.Ip;
    }
}