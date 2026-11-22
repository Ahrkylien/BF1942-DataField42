namespace DataField42.Website.Services.ClientServer;

/// <summary>
/// A request from a client that cannot be carried out: it asked for something that is not there, or
/// broke the protocol. The connection is dropped, and the message is what the request log records.
/// </summary>
public sealed class ClientRequestException(string message) : Exception(message);
