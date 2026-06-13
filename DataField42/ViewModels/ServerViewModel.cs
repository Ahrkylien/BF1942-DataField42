using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataField42.Interfaces;
using DF.Watchable;
using Microsoft.Extensions.Logging;

namespace DataField42.ViewModels;

public partial class ServerViewModel : ObservableObject, IPageViewModel
{
    private readonly Bf1942Server _bf1942Server;
    private readonly Action<ServerViewModel> _onSelectionHandler;
    private readonly Action<ServerViewModel> _onStartSyncHandler;
    private readonly ILogger _logger;
    private readonly string? _storedServerName;

    // ── Core ──────────────────────────────────────────────────────────────────
    public string Name => QueryResult?.HostName ?? _storedServerName ?? string.Empty;
    public string IpAndGamePort => $"{_bf1942Server.Ip}:{QueryResult?.HostPort.ToString() ?? "?"}";
    public string Ip => _bf1942Server.Ip.ToString();
    public int QueryPort => _bf1942Server.QueryPort;
    public int SortKey => QueryResult is null ? -1 : (int)QueryResult.NumberOfPlayers;
    public string GameVersion => QueryResult?.GameVersion ?? string.Empty;

    public string Players => QueryResult is null ? "" : $"{QueryResult.NumberOfPlayers}/{QueryResult.MaximumNumberOfPlayers}";

    public string Map => QueryResult?.MapName ?? string.Empty;
    public string Mod => QueryResult?.Mod ?? string.Empty;
    public string Mode => QueryResult?.GameType ?? string.Empty;

    public bool HasPassword => QueryResult?.HasPassword ?? false;
    public uint NumberOfPlayers => QueryResult?.NumberOfPlayers ?? 0;
    public uint MaximumNumberOfPlayers => QueryResult?.MaximumNumberOfPlayers ?? 0;

    // ── Teams ─────────────────────────────────────────────────────────────────
    public string Team1Name => "Team 1";
    public string Team2Name => "Team 2";
    public string Team1Score => QueryResult is { Tickets1: > 0 } x ? x.Tickets1.ToString() : string.Empty;
    public string Team2Score => QueryResult is { Tickets2: > 0 } x ? x.Tickets2.ToString() : string.Empty;

    // Top-3 for dashboard cards
    public IEnumerable<Player> Team1Players => QueryResult?.Players
        .Where(p => p.Team == 1).OrderByDescending(p => p.Score).Take(3) ?? [];
    public IEnumerable<Player> Team2Players => QueryResult?.Players
        .Where(p => p.Team == 2).OrderByDescending(p => p.Score).Take(3) ?? [];

    // Full list for the server-info view
    public IEnumerable<Player> AllTeam1Players => QueryResult?.Players
        .Where(p => p.Team == 1).OrderByDescending(p => p.Score) ?? Enumerable.Empty<Player>();
    public IEnumerable<Player> AllTeam2Players => QueryResult?.Players
        .Where(p => p.Team == 2).OrderByDescending(p => p.Score) ?? Enumerable.Empty<Player>();

    // ── Round time ────────────────────────────────────────────────────────────
    public int RoundTimeRemain => QueryResult?.RoundTimeRemain ?? 0;
    public int RoundTime => QueryResult?.RoundTime ?? 0;
    public int RoundTimeElapsed => Math.Max(0, RoundTime - RoundTimeRemain);
    public string RoundTimeRemainDisplay => RoundTimeRemain > 0
        ? $"{RoundTimeRemain / 60}m {RoundTimeRemain % 60}s"
        : string.Empty;
    public string RoundTimeElapsedDisplay => RoundTime > 0
        ? $"{RoundTimeElapsed / 60}m {RoundTimeElapsed % 60}s"
        : string.Empty;
    public string RoundTimeTotalDisplay => RoundTime > 0
        ? $"{RoundTime / 60}m {RoundTime % 60}s"
        : string.Empty;

    // ── Bool settings ─────────────────────────────────────────────────────────
    public bool AutoBalanceTeams => QueryResult?.AutoBalanceTeams ?? false;
    public bool HasPunkbuster => QueryResult?.UsesPunkbuster ?? false;
    public bool HitIndicator => QueryResult?.HitIndicator ?? false;
    public bool FreeCamera => QueryResult?.FreeCamera ?? false;
    public bool ExternalView => QueryResult?.ExternalView ?? false;
    public bool AllowNoseCam => QueryResult?.AllowNoseCam ?? false;

    // ── Settings ──────────────────────────────────────────────────────────────
    public int? TicketRatio => QueryResult?.TicketRatio;
    public int? SpawnDelay => QueryResult?.SpawnDelay;
    public int? SpawnWaveTime => QueryResult?.SpawnWaveTime;
    public string TkMode => QueryResult?.TkMode ?? string.Empty;
    public int? KickBack => QueryResult?.KickBack;
    public int? KickBackOnSplash => QueryResult?.KickBackOnSplash;
    public int? SoldierFriendlyFire => QueryResult?.SoldierFriendlyFire;
    public int? SoldierFriendlyFireOnSplash => QueryResult?.SoldierFriendlyFireOnSplash;
    public int? VehicleFriendlyFire => QueryResult?.VehicleFriendlyFire;
    public int? VehicleFriendlyFireOnSplash => QueryResult?.VehicleFriendlyFireOnSplash;
    public int? GameStartDelay => QueryResult?.GameStartDelay;
    public string ActiveMods => QueryResult?.ActiveMods ?? string.Empty;
    public int? TimeLimit => QueryResult?.TimeLimit;

    // ── Always-show when queried ──────────────────────────────────────────────
    public int Tickets1 => QueryResult?.Tickets1 ?? 0;
    public int Tickets2 => QueryResult?.Tickets2 ?? 0;
    public int AverageFps => QueryResult?.AverageFps ?? 0;
    public int BandwidthChokeLimit => QueryResult?.BandwidthChokeLimit ?? 0;
    public int ContentCheck => QueryResult?.ContentCheck ?? 0;
    public int Status => QueryResult?.Status ?? 0;
    public string UnpureModsDisplay => QueryResult is null ? string.Empty
        : QueryResult.UnpureMods.Count == 0 ? "[]"
        : string.Join(", ", QueryResult.UnpureMods);

    // ── Show only when meaningful (non-zero) ──────────────────────────────────
    public string ReservedSlots => QueryResult?.ReservedSlots > 0 ? QueryResult.ReservedSlots.ToString() : string.Empty;
    public string NumberOfRounds => QueryResult?.NumberOfRounds > 0 ? QueryResult.NumberOfRounds.ToString() : string.Empty;
    public string NameTagDistance => QueryResult?.NameTagDistance > 0 ? QueryResult.NameTagDistance.ToString() : string.Empty;
    public string NameTagDistanceScope => QueryResult?.NameTagDistanceScope > 0 ? QueryResult.NameTagDistanceScope.ToString() : string.Empty;
    public string Cpu => QueryResult?.Cpu > 0 ? QueryResult.Cpu.ToString() : string.Empty;
    public string AlliedTeamRatio => QueryResult?.AlliedTeamRatio > 0 ? QueryResult.AlliedTeamRatio.ToString() : string.Empty;
    public string AxisTeamRatio => QueryResult?.AxisTeamRatio > 0 ? QueryResult.AxisTeamRatio.ToString() : string.Empty;
    public string TeamRatio => QueryResult?.AlliedTeamRatio > 0 || QueryResult?.AxisTeamRatio > 0
        ? $"{QueryResult!.AxisTeamRatio}:{QueryResult.AlliedTeamRatio}"
        : string.Empty;
    public string Stage => QueryResult?.GameMode ?? string.Empty;
    public DedicatedServerType? DedicatedServerType => QueryResult?.DedicatedServerType;
    public string? Language => QueryResult?.Language;
    public string Location => QueryResult?.Location?.ToString() ?? string.Empty;

    public Bf1942QueryResult? QueryResult => _bf1942Server.QueryResult;

    // ── Infrastructure ─────────────────────────────────────────────────────────
    public bool HasQueryResult => QueryResult is not null;

    public Watchable<ConnectionState> ConnectionState => _bf1942Server.State;

    [ObservableProperty]
    private bool _errorOccured;

    [ObservableProperty]
    private string? _errorMessage;

    public bool GeneratedItem { get; init; } = false;

    public string Title => Name;

    public event Action? NewQuery;

    public ServerViewModel(
        Bf1942Server bf1942Server,
        Action<ServerViewModel> onSelectionHandler,
        Action<ServerViewModel> onStartSyncHandler,
        ILogger logger,
        string? storedServerName = null,
        bool queryServer = false)
    {
        _bf1942Server = bf1942Server;
        _onSelectionHandler = onSelectionHandler;
        _onStartSyncHandler = onStartSyncHandler;
        _logger = logger;
        _storedServerName = storedServerName;
        bf1942Server.NewQuery += RefreshData;
        if (queryServer)
            _ = Task.Run(Initialize);
    }

    private async Task Initialize()
    {
        try
        {
            await _bf1942Server.QueryServer(TimeSpan.FromSeconds(3));
        }
        catch (TimeoutException)
        {
            ErrorOccured = true;
            ErrorMessage = "Server did not respond in time..";
        }
        catch (Exception ex)
        {
            ErrorOccured = true;
            ErrorMessage = $"During querying of the server an exception occurred: {ex.Message}";
            _logger.LogError(ex, "During querying of the server an exception occurred");
        }
    }

    [RelayCommand]
    private void JoinServer() => _onStartSyncHandler(this);

    [RelayCommand]
    private void Click() => _onSelectionHandler(this);

    private void RefreshData()
    {
        OnPropertyChanged(string.Empty);
        NewQuery?.Invoke();
    }

    public bool Equals(Bf1942Server server) => _bf1942Server == server;
}
