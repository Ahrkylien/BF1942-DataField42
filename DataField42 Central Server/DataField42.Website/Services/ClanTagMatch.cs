namespace DataField42.Website.Services;

/// <summary>
/// A clan tag recognised at the start of a player name.
/// </summary>
public readonly record struct ClanTagMatch(string Tag, string ClanName, string PlayerName);
