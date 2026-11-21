namespace DataField42.Website.Models;

/// <summary>
/// A copy of a file found elsewhere in the archive, with how its date compares to the one being
/// examined.
/// </summary>
public sealed record LinkedFileViewModel(string Path, string? Crc, long Size, DateTime Modified, string DateColor);
