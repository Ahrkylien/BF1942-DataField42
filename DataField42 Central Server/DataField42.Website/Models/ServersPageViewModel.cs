namespace DataField42.Website.Models;

/// <summary>
/// The servers page, which unlike the home page also lists the empty servers -- below a blank row
/// that separates them from the populated ones.
/// </summary>
public sealed class ServersPageViewModel
{
    public int TotalPlayers { get; init; }

    public int ServerCount { get; init; }

    /// <summary>
    /// The rows in display order. A null entry is the separator before the empty servers.
    /// </summary>
    public IReadOnlyList<ServerListEntryViewModel?> Rows { get; init; } = [];
}
