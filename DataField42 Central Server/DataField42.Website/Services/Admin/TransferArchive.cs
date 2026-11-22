namespace DataField42.Website.Services.Admin;

/// <summary>
/// What the uploaded archive of new files holds, as far as it could be read.
/// </summary>
/// <param name="IsUploaded">Whether an archive was uploaded at all.</param>
/// <param name="Problem">Why an uploaded archive could not be read, or <see langword="null"/> if it could.</param>
/// <param name="Entries">The files inside, as paths with forward slashes. Folders are not listed.</param>
public sealed record TransferArchive(bool IsUploaded, string? Problem, IReadOnlySet<string> Entries)
{
    public static TransferArchive NotUploaded { get; } = new(false, null, new HashSet<string>());

    public bool IsReadable => IsUploaded && Problem is null;
}
