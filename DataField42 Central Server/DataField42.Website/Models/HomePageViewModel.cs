namespace DataField42.Website.Models;

/// <summary>
/// The home page: the live server list and the last day's player count.
/// </summary>
public sealed class HomePageViewModel
{
    public int TotalPlayers { get; init; }

    public IReadOnlyList<ServerListEntryViewModel> Servers { get; init; } = [];

    public required LineGraphViewModel PlayerNumbers { get; init; }
}
