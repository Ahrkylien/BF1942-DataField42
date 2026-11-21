namespace DataField42.Website.Services.Admin.Update;

/// <summary>
/// What a branch holds that the running site does not.
/// </summary>
/// <param name="Branch">The branch that was checked.</param>
/// <param name="Tip">The newest commit on it.</param>
/// <param name="IsUpToDate">Whether the running site is already built from <paramref name="Tip"/>.</param>
/// <param name="Commits">
/// The commits the running site lacks, newest first. When the running version is not known, the
/// latest commits of the branch.
/// </param>
/// <param name="ShowsLatestOnly">Whether <paramref name="Commits"/> is the latest few rather than what is new.</param>
public sealed record UpdateCheck(string Branch, string Tip, bool IsUpToDate, IReadOnlyList<CommitInfo> Commits, bool ShowsLatestOnly);
