using DataField42.Website.Services.Admin.Jobs;

namespace DataField42.Website.Models;

/// <summary>
/// One job's card: its state, where to return to after starting or stopping it, and the mods it
/// can be limited to when it takes one.
/// </summary>
public sealed class JobCardViewModel
{
    public required JobStatus Job { get; init; }

    public required string ReturnTo { get; init; }

    /// <summary>
    /// Whether this job can be limited to one mod.
    /// </summary>
    /// <summary>
    /// Why the job cannot be started right now, shown beside a disabled button. A job that is
    /// running can still be stopped.
    /// </summary>
    public string? BlockedReason { get; init; }

    public bool TakesMod { get; init; }

    public IReadOnlyList<string> ModFolderNames { get; init; } = [];
}
