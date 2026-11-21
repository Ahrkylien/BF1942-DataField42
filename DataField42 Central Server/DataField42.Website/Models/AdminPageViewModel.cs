using DataField42.Website.Services.Admin.Jobs;

namespace DataField42.Website.Models;

/// <summary>
/// The admin overview: the maintenance jobs and their current state.
/// </summary>
public sealed class AdminPageViewModel
{
    public IReadOnlyList<JobStatus> Jobs { get; init; } = [];

    /// <summary>
    /// The mods that can be given to the jobs that take one, so a run can be limited to a single
    /// mod rather than the whole collection.
    /// </summary>
    public IReadOnlyList<string> ModFolderNames { get; init; } = [];

    public string? Message { get; init; }

    public string? ErrorMessage { get; init; }
}
