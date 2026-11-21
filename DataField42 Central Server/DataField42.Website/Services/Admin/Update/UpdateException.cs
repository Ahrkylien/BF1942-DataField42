namespace DataField42.Website.Services.Admin.Update;

/// <summary>
/// An update could not be checked or started, for a reason the admin can act on.
/// </summary>
public sealed class UpdateException(string message) : Exception(message);
