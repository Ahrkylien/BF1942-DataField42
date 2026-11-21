using DataField42.Website.Services.Admin.Update;

namespace DataField42.Website.Models;

/// <summary>
/// The admin Update page.
/// </summary>
public sealed class UpdatePageViewModel
{
    /// <summary>
    /// Whether this installation can update itself at all.
    /// </summary>
    public required bool IsAvailable { get; init; }

    public IReadOnlyList<string> Branches { get; init; } = [];

    public required string SelectedBranch { get; init; }

    public required UpdateStatus Status { get; init; }
}
