using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BF1942.Core.MasterServer;

/// <summary>
/// Asks a Battlefield 1942 master server which game servers there are, the way the game does: over TCP on port 28900, with the answer encrypted.
/// </summary>
/// <remarks>
/// The master greets the client with <c>\basic\\secure\...</c>. The client says which game it is and asks for the list with
/// <c>\list\cmp\gamename\bfield1942\final\</c>. The master answers with the list, encrypted with <see cref="Enctype2Decoder"/>, and closes the connection. The
/// list is 6 bytes for each server: its IP address and its query port, which is a big-endian number.
/// </remarks>
public sealed class Bf1942MasterServerClient
{
    public const int DefaultPort = 28900;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The key the list is encrypted with, which is the game's.
    /// </summary>
    private const string GameKey = "HpWx9z";

    /// <summary>
    /// What a client says first. The master does not validate the clients replay yet, so a fixed word (bfMaster) works for now.
    /// </summary>
    private const string HandshakeRequest = "\\gamename\\bfield1942\\gamever\\2\\location\\0\\validate\\bfMaster\\enctype\\2\\final\\\\queryid\\1.1\\";

    private const string ListRequest = "\\list\\cmp\\gamename\\bfield1942\\final\\";

    private const string GreetingStart = "\\basic\\";

    /// <summary>
    /// The size of the biggest list that is accepted. A list holds at most 65,536 servers of 6 bytes, and this leaves room for the header.
    /// </summary>
    private const int MaxResponseLength = 1 << 20;

    private const int EntryLength = 6;

    /// <summary>
    /// The query addresses of the game servers the master knows of.
    /// </summary>
    /// <exception cref="TimeoutException">The master did not answer in time.</exception>
    /// <exception cref="InvalidDataException">What answered is not a master server, or its list is damaged.</exception>
    /// <exception cref="SocketException">The master could not be reached.</exception>
    public async Task<IReadOnlyList<IPEndPoint>> GetServerList(
        string host,
        int port = DefaultPort,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout ?? DefaultTimeout);
        var token = timeoutSource.Token;

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, token);
            var stream = client.GetStream();

            await ReadGreetingAsync(stream, token);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(HandshakeRequest + ListRequest), token);

            var encrypted = await ReadUntilClosedAsync(stream, token);
            return ParseServers(Enctype2Decoder.Decode(GameKey, encrypted));
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The master server {host}:{port} did not answer in time", ex);
        }
    }

    private static async Task ReadGreetingAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        int read = await stream.ReadAsync(buffer, cancellationToken);

        if (!Encoding.ASCII.GetString(buffer, 0, read).StartsWith(GreetingStart, StringComparison.Ordinal))
            throw new InvalidDataException("The server did not greet like a master server");
    }

    private static async Task<byte[]> ReadUntilClosedAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var response = new MemoryStream();
        var buffer = new byte[4096];

        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (response.Length + read > MaxResponseLength)
                throw new InvalidDataException($"The master server's answer is longer than {MaxResponseLength} bytes");

            response.Write(buffer, 0, read);
        }

        return response.ToArray();
    }

    private static List<IPEndPoint> ParseServers(ReadOnlySpan<byte> list)
    {
        if (list.Length % EntryLength != 0)
            throw new InvalidDataException($"The master server's list is {list.Length} bytes, which is not a whole number of servers");

        var servers = new List<IPEndPoint>(list.Length / EntryLength);
        for (int i = 0; i < list.Length; i += EntryLength)
            servers.Add(new IPEndPoint(new IPAddress(list.Slice(i, 4)), BinaryPrimitives.ReadUInt16BigEndian(list.Slice(i + 4, 2))));

        return servers;
    }
}
