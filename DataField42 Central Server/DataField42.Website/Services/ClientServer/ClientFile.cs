namespace DataField42.Website.Services.ClientServer;

/// <summary>
/// A file a client may download: how the protocol describes it, and where it is on this server.
/// </summary>
public sealed record ClientFile(FileInfo Info, string FullPath);
