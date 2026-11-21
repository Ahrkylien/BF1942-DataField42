namespace DataField42.Website.Models;

/// <summary>
/// One file in a folder still to be sorted, and the copies of it found elsewhere.
/// </summary>
public sealed record ToDoFileViewModel(string Name, string? Crc, long Size, DateTime Modified, IReadOnlyList<LinkedFileViewModel> LinkedFiles);
