namespace DataField42.Website.Models;

/// <summary>
/// A map servers play that the file database does not have, with the tracked servers it was seen
/// on.
/// </summary>
public sealed record MissingMapViewModel(string Name, IReadOnlyList<int> ServerIds);
