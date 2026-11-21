namespace DataField42.Website.Models;

/// <summary>
/// An uploaded file on the transfer page.
/// </summary>
public sealed record UploadedFileViewModel(string Name, long Size, DateTime ModifiedUtc);
