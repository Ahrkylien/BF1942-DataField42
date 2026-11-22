using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Communication TCP protocol layer between client and server.
/// Each 'packet' starts with a 4-byte int indicating the packet size (remaining part).
/// Except for file transfer.
/// </summary>
public class DataField42Communication : TcpCommunicationBase
{
    public const string CentralDbDomainName = "files.bf1942.eu";
    public const int DefaultPort = 28901;

    private bool _sessionIsUsed = false;

    public DataField42Communication(ILogger<DataField42Communication> logger) : base(CentralDbDomainName, DefaultPort, logger) { }

    public DataField42Communication(string domainName, ILogger<DataField42Communication> logger) : base(domainName, DefaultPort, logger) { }

    public DataField42Communication(IPAddress ipAddress, ILogger<DataField42Communication> logger) : base(ipAddress.ToString(), DefaultPort, logger) { }

    public void StartSession()
    {
        if (_sessionIsUsed)
        {
            _logger.LogDebug($"Session already used — renewing TCP connection to {_domainNameOrIp}.");
            RenewConnection();
        }
        _sessionIsUsed = false;
        _logger.LogDebug($"Session started for {_domainNameOrIp}:{_port}.");
    }

    public async Task<(IPAddress?, Version)> HandShake(Version version)
    {
        _logger.LogDebug($"Sending handshake with version {version} to {_domainNameOrIp}.");
        SendString($"handshake {version}");
        var responseData = await ReceiveSpaceSeperatedString(2);
        var redirectServerIp = responseData.ElementAt(0) == "null" ? null : IPAddress.Parse(responseData.ElementAt(0));
        var versionReceived = new Version(responseData.ElementAt(1));
        _logger.LogDebug($"Handshake response — redirect: {redirectServerIp?.ToString() ?? "none"}, serverVersion: {versionReceived}.");
        return (redirectServerIp, versionReceived);
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
        TimeSpan? timeoutDuration = null)
    {
        if (timeoutDuration == null)
            timeoutDuration = TimeSpan.FromSeconds(4);

        _logger.LogDebug($"Receiving file of {length} bytes from {_domainNameOrIp}.");

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

            if (stopWatch.Elapsed > timeoutDuration)
                throw new TimeoutException($"Can't receive data in time ({timeoutDuration} ms)");
        }

        _logger.LogDebug($"File receive complete — {totalReceivedLength} bytes received.");
    }

    public async Task<string> ReceiveString() => await receiveString(await receiveDataLength());

    public async Task<int> ReceiveInt() => int.Parse(await receiveString(await receiveDataLength()));

    public async Task<ulong> ReceiveUlong() => ulong.Parse(await receiveString(await receiveDataLength()));

    public async Task<IEnumerable<string>> ReceiveSpaceSeperatedString(uint expectNumber)
    {
        var lengthToReceive = await receiveDataLength();
        var list = (await receiveString(lengthToReceive)).Split(' ').ToList();
        if (list.Count != expectNumber)
            throw new Exception($"Expected number spaces in space separated string is {list.Count} while it should be {expectNumber}");
        return list;
    }

    public async Task<FileInfo> ReceiveFileInfo() => new(await ReceiveSpaceSeperatedString(5));

    [return: NotNull]
    public async Task<List<FileInfo>> ReceiveFileInfos(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug($"Waiting to receive file info list from {_domainNameOrIp}.");
        var fileInfos = new List<FileInfo>();
        var lengthToReceive = await receiveDataLength(TimeSpan.FromMinutes(2), cancellationToken);
        if (lengthToReceive == 0)
        {
            _logger.LogDebug("Received empty file info list.");
            return fileInfos;
        }

        var fileInfoStrings = (await receiveString(lengthToReceive, cancellationToken)).Split('\n');
        foreach (var fileInfoString in fileInfoStrings)
        {
            var fileInfoStringItems = fileInfoString.Split(' ').ToList();
            fileInfos.Add(new FileInfo(fileInfoStringItems));
        }

        _logger.LogInformation($"Received {fileInfos.Count} file infos from {_domainNameOrIp}.");
        return fileInfos;
    }

    public void SendBytes(byte[] data)
    {
        _sessionIsUsed = true;
        _messages.SendMessage(data);
    }

    public void SendString(string message) => SendBytes(System.Text.Encoding.UTF8.GetBytes(message));

    public void SendAcknowledgement() => SendString(DataField42MessageStream.Acknowledgement);
}

public class TcpCommunicationBase : IDisposable
{
    public string DisplayName => _domainNameOrIp;

    protected string _domainNameOrIp;
    protected int _port;
    protected TcpClient _client;
    protected NetworkStream _stream;
    protected DataField42MessageStream _messages;
    protected readonly ILogger _logger;

    private const int _timeOutInitialConnect = 1000; // ms

    public TcpCommunicationBase(string domainNameOrIp, int port, ILogger logger)
    {
        _domainNameOrIp = domainNameOrIp;
        _port = port;
        _logger = logger;

        _logger.LogDebug($"Connecting to {domainNameOrIp}:{port}.");
        _connect();
        _logger.LogInformation($"Connected to {domainNameOrIp}:{port}.");
    }

    [MemberNotNull(nameof(_client))]
    [MemberNotNull(nameof(_stream))]
    [MemberNotNull(nameof(_messages))]
    private void _connect()
    {
        _client = new TcpClient();
        if (!_client.ConnectAsync(_domainNameOrIp, _port).Wait(_timeOutInitialConnect))
            throw new TimeoutException($"Server {_domainNameOrIp}:{_port} doesn't respond in {_timeOutInitialConnect} ms");
        _stream = _client.GetStream();
        _messages = new DataField42MessageStream(_stream);
    }

    protected void RenewConnection()
    {
        _logger.LogDebug($"Renewing connection to {_domainNameOrIp}:{_port}.");
        Dispose();
        _connect();
    }

    protected async Task<(int, byte[])> tryReceiveBytes(int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var numberOfBytes = await _stream.ReadAsync(new Memory<byte>(buffer), cancellationToken);
        return (numberOfBytes, buffer);
    }

    protected Task<byte[]> receiveBytes(int length, TimeSpan? timeoutTime = null, CancellationToken cancellationToken = default) =>
        _messages.ReceiveBytes(length, timeoutTime, cancellationToken);

    protected Task<int> receiveDataLength(TimeSpan? timeoutTime = null, CancellationToken cancellationToken = default) =>
        _messages.ReceiveDataLength(timeoutTime, cancellationToken);

    protected Task<string> receiveString(int length, CancellationToken cancellationToken = default) =>
        _messages.ReceiveString(length, cancellationToken: cancellationToken);

    public void Dispose()
    {
        _logger.LogDebug($"Disposing TCP connection to {_domainNameOrIp}:{_port}.");
        _stream.Close();
        _client.Close();
    }
}
