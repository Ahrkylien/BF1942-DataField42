namespace DataField42.Website.Services.Admin.Update;

/// <summary>
/// The state of the last update, and what it wrote to its log.
/// </summary>
public sealed record UpdateStatus(UpdateState State, DateTime? ChangedUtc, string Log, DeployedVersion? Deployed);
