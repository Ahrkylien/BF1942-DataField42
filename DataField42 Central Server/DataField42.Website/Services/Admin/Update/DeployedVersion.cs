namespace DataField42.Website.Services.Admin.Update;

/// <summary>
/// The branch and commit the running site was built from. The branch is not known for a build that
/// was not made by an update or the install script.
/// </summary>
public sealed record DeployedVersion(string? Branch, string Commit)
{
    public string ShortCommit => Commit.Length > 8 ? Commit[..8] : Commit;
}
