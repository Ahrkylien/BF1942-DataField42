using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Sockets;
using System;

/// <summary>
/// Communication tcp protocol layer between client and server.
/// Each 'packet' starts with a 4 byte int indicating the packet size (remaining part).
/// Except for file tranfer.
/// </summary>
public class DataField42Communication : TcpCommunicationBase
{
    public const string CentralDbDomainName = "files.bf1942.eu";
    public const int DefaultPort = 28901;

    private bool _sessionIsUsed = false;


    public DataField42Communication() : base(CentralDbDomainName, DefaultPort) { }

    public DataField42Communication(string domainName) : base(domainName, DefaultPort) { }

    public DataField42Communication(string domainName, int port) : base(domainName, port) { }

    public void StartSession()
    {
        if(_sessionIsUsed)
            RenewConnection();
        _sessionIsUsed = false;
    }

    public async Task ReceiveFile(ulong length, FileStream fileStream, DownloadBackgroundWorker backgroundWorker, CancellationToken cancellationToken)
    {
        await ReceiveFile(length, fileStream, new List<DownloadBackgroundWorker>() { backgroundWorker }, cancellationToken);
    }

    public async Task ReceiveFile(
        ulong length,
        FileStream fileStream,
        IEnumerable<DownloadBackgroundWorker> backgroundWorkers,
        CancellationToken cancellationToken,
        int millisecondsWithoutReceivingBeforeTimeout = 1000)
    {
        ulong totalReceivedLength = 0;
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        while (totalReceivedLength != length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (var receivedLength, var data) = await tryReceiveBytes(4096, cancellationToken);
            fileStream.Write(data, 0, receivedLength);
            totalReceivedLength += (ulong)receivedLength;

            foreach (var backgroundWorker in backgroundWorkers)
                backgroundWorker.ReportProgressAmount((ulong)receivedLength);

            if (receivedLength != 0)
                stopWatch.Restart();

            if (stopWatch.ElapsedMilliseconds > millisecondsWithoutReceivingBeforeTimeout)
                throw new TimeoutException($"Can't receive data in time ({millisecondsWithoutReceivingBeforeTimeout} ms)");
        }
    }


    public async Task<string> ReceiveString() => await receiveString(await receiveDataLength());

    public async Task<int> ReceiveInt() => int.Parse(await receiveString(await receiveDataLength()));

    public async Task<ulong> ReceiveUlong() => ulong.Parse(await receiveString(await receiveDataLength()));

    public async Task<IEnumerable<string>> ReceiveSpaceSeperatedString(uint expectNumber)
    {
        var lengthToReceive = await receiveDataLength();
        var list = (await receiveString(lengthToReceive)).Split(' ').ToList();
        if (list.Count != expectNumber)
            throw new Exception($"Expected number spaces in space seperated string is {list.Count} while it should be {expectNumber}");
        return list;
    }

    public async Task<FileInfo> ReceiveFileInfo() => new(await ReceiveSpaceSeperatedString(5));

    [return: NotNull]
    public async Task<List<FileInfo>> ReceiveFileInfos(CancellationToken cancellationToken = default)
    {
        var fileInfos = new List<FileInfo>();
        var lengthToReceive = await receiveDataLength(cancellationToken);
        if (lengthToReceive == 0)
            return fileInfos;
        
        var fileInfoSrings = (await receiveString(lengthToReceive, cancellationToken)).Split('\n');
        foreach (var fileInfoString in fileInfoSrings)
        {
            var fileInfoStringItems = fileInfoString.Split(' ').ToList();
            fileInfos.Add(new FileInfo(fileInfoStringItems));
        }
        return fileInfos;
    }

    public void SendBytes(byte[] data)
    {
        _sessionIsUsed = true;
        sendInt(data.Length);
        _stream.Write(data);
    }

    public void SendString(string message) => SendBytes(System.Text.Encoding.ASCII.GetBytes(message));

    public void SendAcknowledgement() => SendString("ok");
}

public class TcpCommunicationBase : IDisposable
{
    public string DisplayName => _domainNameOrIp;

    protected string _domainNameOrIp;
    protected int _port;
    protected TcpClient _client;
    protected NetworkStream _stream;

    private const int _timeOutInitialConnect = 1000; // ms

    public TcpCommunicationBase(string domainNameOrIp, int port)
    {
        _domainNameOrIp = domainNameOrIp;
        _port = port;

        _connect();
    }

    [MemberNotNull(nameof(_client))]
    [MemberNotNull(nameof(_stream))]
    private void _connect(int millisecondsWithoutReceivingBeforeTimout = 1000)
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
        _connect();
    }

    protected async Task<(int, byte[])> tryReceiveBytes(int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var numberOfBytes = await _stream.ReadAsync(new Memory<byte>(buffer), cancellationToken);
        return (numberOfBytes, buffer);
    }

    protected async Task<byte[]> receiveBytes(int length, CancellationToken cancellationToken = default, int millisecondsWithoutReceivingBeforeTimout = 1000)
    {
        // TODO: remove double timout (custom and buildin)
        int bytesRead = 0;
        var buffer = new byte[length];

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        while (bytesRead < length)
        {
            int bytesReadThisIteration = await _stream.ReadAsync(buffer.AsMemory(bytesRead, length - bytesRead));

            bytesRead += bytesReadThisIteration;

            if (bytesReadThisIteration != 0)
                stopWatch.Restart();

            if (stopWatch.ElapsedMilliseconds > millisecondsWithoutReceivingBeforeTimout)
                throw new TimeoutException($"Can't receive data in time ({millisecondsWithoutReceivingBeforeTimout} ms)");
        }
        return buffer;
    }

    protected async Task<int> receiveDataLength(CancellationToken cancellationToken = default)
    {
        var data = await receiveBytes(4, cancellationToken);
        return BitConverter.ToInt32(data);
    }

    protected async Task<string> receiveString(int length, CancellationToken cancellationToken = default)
    {
        var data = await receiveBytes(length, cancellationToken);
        return System.Text.Encoding.ASCII.GetString(data, 0, length);
    }

    protected void sendInt(int value)
    {
        _stream.Write(BitConverter.GetBytes(value));
    }

    public void Dispose()
    {
        _stream.Close();
        _client.Close();
    }
}