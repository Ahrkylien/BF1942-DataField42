using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DataField42.ViewModels;

public partial class ServerViewModel : ObservableObject
{
    private readonly Bf1942Server _bf1942Server;
    private readonly Action<ServerViewModel> _onSelectionHandler;
    private readonly string? _storedServerName;

    public string Name => QueryResult?.HostName ?? _storedServerName ?? string.Empty;

    public string IpAndGamePort => $"{_bf1942Server.Ip}:{QueryResult?.HostPort.ToString() ?? "xxxxx"}";

    public string Ip => _bf1942Server.Ip.ToString();

    public int QueryPort => _bf1942Server.QueryPort;

    public int SortKey => QueryResult is null ? -1 : (int)QueryResult.NumberOfPlayers;

    public string Players
    {
        get
        {
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

    [ObservableProperty]
    public bool _loading;

    [ObservableProperty]
    public bool _errorOccured;

    [ObservableProperty]
    public string? _errorMessage;

    public bool GeneratedItem { get; init; } = false;

    public event Action NewQuery;

    public ServerViewModel(Bf1942Server bf1942Server,
                           Action<ServerViewModel> onSelectionHandler,
                           string? storedServerName = null,
                           bool queryServer = false)
    {
        _bf1942Server = bf1942Server;
        _onSelectionHandler = onSelectionHandler;
        _storedServerName = storedServerName;
        bf1942Server.NewQuery += RefreshData;
        Loading = true;
        if (queryServer)
            _ = Task.Run(() => Initialize());
    }

    private async Task Initialize()
    {
        try
        {
            await _bf1942Server.QueryServer(TimeSpan.FromMilliseconds(99999));
        }
        catch (TimeoutException)
        {
            ErrorOccured = true;
            ErrorMessage = "Server did not respond in time..";
        }
        catch (Exception ex)
        {
            ErrorOccured = true;
            ErrorMessage = $"During querying of the server an exception occured: {ex.Message}";
        }
        finally
        {
            Loading = false;
        }
    }

    [RelayCommand]
    private void Click()
    {
        _onSelectionHandler(this);
    }

    private void RefreshData()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IpAndGamePort));
        OnPropertyChanged(nameof(Players));
        OnPropertyChanged(nameof(Map));
        OnPropertyChanged(nameof(Mod));
        OnPropertyChanged(nameof(SortKey));
        Loading = false;
        NewQuery?.Invoke();
    }

    public bool Equals(Bf1942Server server) => _bf1942Server == server;
}