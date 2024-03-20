using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

public class TcpClientBase : IDisposable
{
    public string DisplayName => _domainNameOrIp;

    protected string _domainNameOrIp;
    protected int _port;
    protected TcpClient _client;
    protected NetworkStream _stream;

    private const int _timeOutInitialConnect = 1000; // ms

    public TcpClientBase(string domainNameOrIp, int port)
    {
        _domainNameOrIp = domainNameOrIp;
        _port = port;

        Connect();
    }

    [MemberNotNull(nameof(_client))]
    [MemberNotNull(nameof(_stream))]
    private void Connect()
    {
        _client = new TcpClient();
        if (!_client.ConnectAsync(_domainNameOrIp, _port).Wait(_timeOutInitialConnect))
            throw new TimeoutException($"Server {_domainNameOrIp}:{_port} doesn't respond in {_timeOutInitialConnect} ms");
        _stream = _client.GetStream();
        // _stream.ReadTimeout = millisecondsWithoutReceivingBeforeTimout;
    }

    protected void RenewConnection()
    {
        Dispose();
        Connect();
    }

    protected async Task<byte[]> ReceiveBytes(int length, TimeSpan? timeoutTime = null, CancellationToken cancellationToken = default)
    {
        if (timeoutTime == null)
            timeoutTime = TimeSpan.FromSeconds(10);

        // TODO: remove double timout (custom and buildin)
        int bytesRead = 0;
        var buffer = new byte[length];

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        while (bytesRead < length)
        {
            int bytesReadThisIteration = await _stream.ReadAsync(buffer.AsMemory(bytesRead, length - bytesRead), cancellationToken);

            bytesRead += bytesReadThisIteration;

            if (bytesReadThisIteration != 0)
                stopWatch.Restart();

            if (stopWatch.Elapsed > timeoutTime)
                throw new TimeoutException($"Can't receive data in time ({timeoutTime?.TotalSeconds} seconds)");
        }
        return buffer;
    }

    protected async Task<string> ReceiveString(int length, CancellationToken cancellationToken = default)
    {
        var data = await ReceiveBytes(length, cancellationToken: cancellationToken);
        return System.Text.Encoding.ASCII.GetString(data, 0, length);
    }

    protected async Task<UInt32> ReceiveUInt32(TimeSpan? timeoutTime = null, CancellationToken cancellationToken = default)
    {
        if (timeoutTime == null)
            timeoutTime = TimeSpan.FromSeconds(10);

        var data = await ReceiveBytes(4, timeoutTime, cancellationToken);
        return BitConverter.ToUInt32(data);
    }

    protected void SendBytes(byte[] data) => _stream.Write(data);

    protected void SendByte(byte data) => SendBytes(new byte[] { data });

    protected void SendUInt32(UInt32 data) => SendBytes(BitConverter.GetBytes(data));

    protected void SendString(string data) => SendBytes(Encoding.UTF8.GetBytes(data));

    public void Dispose()
    {
        _stream.Close();
        _client.Close();
    }
}