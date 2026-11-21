namespace DataField42.Website.Models;

/// <summary>
/// One archive of a map, as the map page lists it.
/// </summary>
public sealed record MapRfaViewModel(int RfaId, string FileName, DateTime? Date);
