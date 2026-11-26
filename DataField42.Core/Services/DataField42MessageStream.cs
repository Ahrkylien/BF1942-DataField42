using System.Buffers.Binary;
using System.Text;

/// <summary>
/// The messages of the DataField42 protocol over a stream, for either side of it: a client talking to
/// a server, and a server answering a client.
/// </summary>
/// <remarks>
/// A message is a 4-byte little-endian length and that many bytes of UTF-8 text. A file is not a
/// message: it is its bytes alone, after a message that gave its size. Most messages are answered with
/// the message <c>ok</c>.
/// Whatever is received or sent has to make progress: the other side may stay silent, or leave a write
/// stalled, for the stream's timeout and no longer.
/// </remarks>
/// <param name="defaultTimeout">How long the other side may take, when a call does not say; <see cref="DefaultTimeout"/> if omitted.</param>
public sealed class DataField42MessageStream(Stream stream, TimeSpan? defaultTimeout = null)
{
    public const string Acknowledgement = "ok";

    /// <summary>
    /// The longest message that is accepted unless a call says otherwise, which is far more than any
    /// request or file list needs.
    /// </summary>
    public const int MaxMessageLength = 1 << 20;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private const int FileChunkSize = 64 * 1024;

    /// <summary>
    /// Receives exactly <paramref name="length"/> bytes. <paramref name="timeout"/> is how long the
    /// other side may stay silent, not how long the whole receive may take.
    /// </summary>
    /// <exception cref="TimeoutException">Nothing arrived for <paramref name="timeout"/>.</exception>
    /// <exception cref="EndOfStreamException">The other side closed the connection.</exception>
    public async Task<byte[]> ReceiveBytes(int length, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[length];

        for (int received = 0; received < length;)
        {
            int read = 0;
            await Within(timeout, cancellationToken, async token => read = await stream.ReadAsync(buffer.AsMemory(received), token));

            if (read == 0)
                throw new EndOfStreamException("The other side closed the connection");

            received += read;
        }

        return buffer;
    }

    /// <summary>
    /// Receives whatever has arrived, which is at most <paramref name="maxLength"/> bytes and none if
    /// the other side closed the connection.
    /// </summary>
    public async Task<byte[]> ReceiveAvailable(int maxLength, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[maxLength];

        int read = 0;
        await Within(timeout, cancellationToken, async token => read = await stream.ReadAsync(buffer, token));

        return buffer[..read];
    }

    public async Task<int> ReceiveDataLength(TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
        BinaryPrimitives.ReadInt32LittleEndian(await ReceiveBytes(4, timeout, cancellationToken));

    /// <summary>
    /// Receives <paramref name="length"/> bytes as text. A length above <paramref name="maxLength"/>
    /// is refused before anything is allocated, so a peer cannot make this reserve memory it will
    /// never fill.
    /// </summary>
    public async Task<string> ReceiveString(int length, TimeSpan? timeout = null, CancellationToken cancellationToken = default, int maxLength = MaxMessageLength)
    {
        if (length < 0 || length > maxLength)
            throw new InvalidDataException($"A message of {length} bytes is not allowed");

        return Encoding.UTF8.GetString(await ReceiveBytes(length, timeout, cancellationToken));
    }

    /// <summary>
    /// Receives one message: its length, then its text.
    /// </summary>
    public async Task<string> ReceiveMessage(TimeSpan? timeout = null, CancellationToken cancellationToken = default, int maxLength = MaxMessageLength) =>
        await ReceiveString(await ReceiveDataLength(timeout, cancellationToken), timeout, cancellationToken, maxLength);

    public void SendMessage(byte[] message) => stream.Write(Frame(message));

    public void SendMessage(string message) => SendMessage(Encoding.UTF8.GetBytes(message));

    public Task SendMessageAsync(string message, CancellationToken cancellationToken = default) =>
        SendBytes(Frame(Encoding.UTF8.GetBytes(message)), cancellationToken);

    /// <summary>
    /// Sends bytes as they are, without a length.
    /// </summary>
    public Task SendBytes(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) =>
        Within(null, cancellationToken, token => stream.WriteAsync(data, token));

    /// <summary>
    /// Sends exactly <paramref name="length"/> bytes of a file, which is the size the other side was
    /// told. A file that is no longer that long fails the send rather than leaving the other side
    /// waiting for the rest.
    /// </summary>
    public async Task SendFile(string path, long length, CancellationToken cancellationToken = default)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileChunkSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[FileChunkSize];

        for (long remaining = length; remaining > 0;)
        {
            int read = await file.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0)
                throw new IOException($"{Path.GetFileName(path)} is shorter than the {length} bytes it was announced with");

            await SendBytes(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    public void SendAcknowledgement() => SendMessage(Acknowledgement);

    public Task SendAcknowledgementAsync(CancellationToken cancellationToken = default) => SendMessageAsync(Acknowledgement, cancellationToken);

    /// <summary>
    /// Sends a message and waits for the other side to acknowledge it.
    /// </summary>
    public async Task SendMessageAndAwaitAcknowledgement(string message, CancellationToken cancellationToken = default)
    {
        await SendMessageAsync(message, cancellationToken);
        await AwaitAcknowledgement(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Waits for the other side to acknowledge what it was sent.
    /// </summary>
    public async Task AwaitAcknowledgement(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (await ReceiveMessage(timeout, cancellationToken, Acknowledgement.Length) != Acknowledgement)
            throw new InvalidDataException("The other side did not acknowledge");
    }

    // The length and the message go out in one write, so they are one packet.
    private static byte[] Frame(byte[] message)
    {
        var frame = new byte[4 + message.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, message.Length);
        message.CopyTo(frame, 4);
        return frame;
    }

    /// <summary>
    /// Runs one read or write, and fails it with a timeout if the other side takes too long.
    /// </summary>
    private async Task Within(TimeSpan? timeout, CancellationToken cancellationToken, Func<CancellationToken, ValueTask> action)
    {
        var limit = timeout ?? defaultTimeout ?? DefaultTimeout;

        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(limit);

        try
        {
            await action(source.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The other side did not respond within {limit.TotalSeconds:0} seconds");
        }
    }
}
