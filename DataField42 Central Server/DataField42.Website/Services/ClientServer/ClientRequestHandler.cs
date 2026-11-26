using System.Buffers.Binary;
using System.Text;

namespace DataField42.Website.Services.ClientServer;

/// <summary>
/// Reads what a client asks for and answers it: its version, an update, or the files for a map.
/// </summary>
/// <remarks>
/// A request is a message of words, the first of which names it. A first-generation client sends the
/// words bare, without a length, and is recognised by its first word; it can only be told its
/// version and be sent the updater that brings it up to date. See
/// <c>docs/first-generation clients.md</c>.
/// </remarks>
public sealed class ClientRequestHandler(
    ReleaseFiles releases,
    HeartbeatRegistry heartbeats,
    DownloadHandler downloads,
    ILogger<ClientRequestHandler> logger
)
{
    private const string CurrentProtocolVersion = "2.1.0.0";
    private const string LegacyProtocolVersion = "2";

    /// <summary>
    /// The longest request there is: a name and a handful of words. Nothing longer is read.
    /// </summary>
    private const int MaxRequestLength = 4096;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How the first words of the three requests a first-generation client makes begin.
    /// </summary>
    private static readonly string[] LegacyRequestStarts = ["upda", "hand", "down"];

    public async Task Handle(DataField42MessageStream messages, string clientIp, CancellationToken cancellationToken)
    {
        var (text, legacy) = await ReadRequest(messages, cancellationToken);

        // Whitespace is what the words are split on; any other control character is a client that is
        // not speaking the protocol, and would only end up in the log.
        if (text.Any(character => char.IsControl(character) && !char.IsWhiteSpace(character)))
            throw new ClientRequestException("A request with control characters");

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            throw new ClientRequestException("An empty request");

        string name = legacy ? words[0] + "V1" : words[0];
        var arguments = words[1..];

        switch (name, arguments.Length)
        {
            case ("handshake", 1):
                await messages.SendMessageAsync(arguments[0] == CurrentProtocolVersion ? $"null {ReleaseFiles.ClientVersion}" : ReleaseFiles.ClientVersion, cancellationToken);
                break;
            case ("update", 1):
                await SendReleaseFile(messages, releases.ClientUpdater, cancellationToken);
                break;
            case ("updateServer", 1):
                await SendProgram(messages, releases.ServerScript.Length, () => messages.SendBytes(releases.ServerScript, cancellationToken), cancellationToken);
                break;
            case ("updateFile", 1):
                await UpdateFile(messages, arguments[0], cancellationToken);
                break;
            case ("heartbeatServer", 1):
                await messages.SendMessageAsync(releases.ServerVersion, cancellationToken);
                heartbeats.Store(clientIp, arguments[0]);
                break;
            case ("download", >= 5):
                await downloads.Handle(messages, clientIp, arguments, cancellationToken);
                break;
            case ("handshakeV1", 1):
                await messages.SendBytes(Encoding.UTF8.GetBytes(LegacyProtocolVersion), cancellationToken);
                break;
            case ("updateV1", 1):
                await SendLegacyUpdate(messages, cancellationToken);
                break;
            default:
                logger.LogInformation("{Ip} sent a request that is not understood: {Name} with {Count} arguments", clientIp, name, arguments.Length);
                break;
        }
    }

    /// <summary>
    /// Reads the request. A client sends it as a message; a first-generation client sends its words
    /// bare, and is recognised by how they begin, which no message length looks like.
    /// </summary>
    private static async Task<(string Text, bool IsLegacy)> ReadRequest(DataField42MessageStream messages, CancellationToken cancellationToken)
    {
        byte[] start = await messages.ReceiveBytes(4, RequestTimeout, cancellationToken);

        if (LegacyRequestStarts.Contains(Encoding.ASCII.GetString(start)))
        {
            byte[] rest = await messages.ReceiveAvailable(MaxRequestLength - start.Length, RequestTimeout, cancellationToken);
            return (Encoding.UTF8.GetString(start) + Encoding.UTF8.GetString(rest), true);
        }

        int length = BinaryPrimitives.ReadInt32LittleEndian(start);
        return (await messages.ReceiveString(length, RequestTimeout, cancellationToken, MaxRequestLength), false);
    }

    private async Task UpdateFile(DataField42MessageStream messages, string fileName, CancellationToken cancellationToken)
    {
        if (fileName != ReleaseFiles.LatestClientFileName)
        {
            logger.LogWarning("A client asked for {FileName}, which is not a file it may have", fileName);
            return;
        }

        await SendReleaseFile(messages, releases.LatestClientFile, cancellationToken);
    }

    private static async Task SendReleaseFile(DataField42MessageStream messages, string path, CancellationToken cancellationToken)
    {
        long length = LengthOf(path);
        await SendProgram(messages, length, () => messages.SendFile(path, length, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Sends a program: its size, and once that is acknowledged, the program, and once that is.
    /// </summary>
    private static async Task SendProgram(DataField42MessageStream messages, long length, Func<Task> sendProgram, CancellationToken cancellationToken)
    {
        await messages.SendMessageAndAwaitAcknowledgement(length.ToString(), cancellationToken);
        await sendProgram();
        await messages.AwaitAcknowledgement(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The same, for a client that sends and expects its words bare.
    /// </summary>
    private async Task SendLegacyUpdate(DataField42MessageStream messages, CancellationToken cancellationToken)
    {
        string path = releases.LegacyClientUpdater;
        long length = LengthOf(path);

        await messages.SendBytes(Encoding.UTF8.GetBytes(length.ToString()), cancellationToken);
        byte[] answer = await messages.ReceiveAvailable(MaxRequestLength, RequestTimeout, cancellationToken);
        if (Encoding.UTF8.GetString(answer) == DataField42MessageStream.Acknowledgement)
            await messages.SendFile(path, length, cancellationToken);
    }

    private static long LengthOf(string path) =>
        File.Exists(path) ? new System.IO.FileInfo(path).Length : throw new ClientRequestException($"There is no release file {path}");
}
