using DataField42.Website.Services.Admin.Jobs;

namespace DataField42.Website.Models;

/// <summary>
/// The page listing the runs kept of one admin job.
/// </summary>
public sealed class JobRunsPageViewModel
{
    public required string JobId { get; init; }

    public required string JobName { get; init; }

    public IReadOnlyList<JobRunSummary> Runs { get; init; } = [];
}
