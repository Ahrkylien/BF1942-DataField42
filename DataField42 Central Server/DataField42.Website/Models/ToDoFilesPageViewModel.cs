namespace DataField42.Website.Models;

/// <summary>
/// The folders still to be sorted.
/// </summary>
public sealed class ToDoFilesPageViewModel
{
    public IReadOnlyList<ToDoFolderViewModel> Folders { get; init; } = [];
}
