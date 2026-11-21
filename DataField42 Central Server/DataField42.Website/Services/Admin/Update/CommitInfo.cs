namespace DataField42.Website.Services.Admin.Update;

/// <summary>
/// One commit, as the Update page lists it.
/// </summary>
public sealed record CommitInfo(string Hash, string Author, DateTime DateUtc, string Subject)
{
    public string ShortHash => Hash.Length > 8 ? Hash[..8] : Hash;
}
