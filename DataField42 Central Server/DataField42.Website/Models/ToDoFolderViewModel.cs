namespace DataField42.Website.Models;

/// <summary>
/// A folder of downloads still to be sorted into the collection.
/// </summary>
public sealed record ToDoFolderViewModel(string Name, string Color, IReadOnlyList<ToDoFileViewModel> Files);
