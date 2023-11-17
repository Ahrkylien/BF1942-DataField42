using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;


/// <summary>
/// Communication tcp protocol layer between client and server.
/// Each 'packet' starts with a 4 byte int indicating the packet size (remaining part).
/// Except for file tranfer.
/// </summary>
public class DataField42Communication : TcpCommunicationBase
{
    public const string CentralDbDomainName = "files.bf1942.eu";
    public const int DefaultPort = 28902;

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

    public void ReceiveFile(ulong length, FileStream fileStream, DownloadBackgroundWorker backgroundWorker)
    {
        ReceiveFile(length, fileStream, new List<DownloadBackgroundWorker>() { backgroundWorker });
    }

    public void ReceiveFile(ulong length, FileStream fileStream, IEnumerable<DownloadBackgroundWorker> backgroundWorkers, int millisecondsWithoutReceivingBeforeTimout = 1000)
    {

        ulong totalReceivedLenth = 0;

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        while (totalReceivedLenth != length)
        {
            (var receivedLenth, var data) = tryReceiveBytes(4096);
            fileStream.Write(data, 0, receivedLenth);
            totalReceivedLenth += (ulong)receivedLenth;
            foreach (var backgroundWorker in backgroundWorkers)
                backgroundWorker.ReportProgressAmount((ulong)receivedLenth);

            if (receivedLenth != 0)
                stopWatch.Restart();

            if (stopWatch.ElapsedMilliseconds > millisecondsWithoutReceivingBeforeTimout)
                throw new TimeoutException($"Can't receive data in time ({millisecondsWithoutReceivingBeforeTimout} ms)");
        }
    }

    public string ReceiveString() => receiveString(receiveDataLength());

    public int ReceiveInt() => int.Parse(receiveString(receiveDataLength()));

    public ulong ReceiveUlong() => ulong.Parse(receiveString(receiveDataLength()));

    public IEnumerable<string> ReceiveSpaceSeperatedString(uint expectNumber)
    {
        var lengthToReceive = receiveDataLength();
        var list = receiveString(lengthToReceive).Split(' ').ToList();
        if (list.Count != expectNumber)
            throw new Exception($"Expected number spaces in space seperated string is {list.Count} while it should be {expectNumber}");
        return list;
    }

    public FileInfo ReceiveFileInfo() => new(ReceiveSpaceSeperatedString(5));

    [return: NotNull]
    public List<FileInfo> ReceiveFileInfos()
    {
        var fileInfos = new List<FileInfo>();
        var lengthToReceive = receiveDataLength();
        if (lengthToReceive == 0)
            return fileInfos;
        
        var fileInfoSrings = receiveString(lengthToReceive).Split('\n');
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

    protected (int, byte[]) tryReceiveBytes(int length)
    {
        var data = new byte[length];
        int numberOfBytes = _stream.Read(data, 0, length);
        return (numberOfBytes, data);
    }

    protected byte[] receiveBytes(int length, int millisecondsWithoutReceivingBeforeTimout = 1000)
    {
        // TODO: remove double timout (custom and buildin)
        int bytesRead = 0;
        byte[] buffer = new byte[length];

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        while (bytesRead < length)
        {
            int bytesReadThisIteration = _stream.Read(buffer, bytesRead, length - bytesRead);

            bytesRead += bytesReadThisIteration;

            if (bytesReadThisIteration != 0)
                stopWatch.Restart();

            if (stopWatch.ElapsedMilliseconds > millisecondsWithoutReceivingBeforeTimout)
                throw new TimeoutException($"Can't receive data in time ({millisecondsWithoutReceivingBeforeTimout} ms)");
        }
        return buffer;
    }

    protected int receiveDataLength()
    {
        var data = receiveBytes(4);
        return BitConverter.ToInt32(data);
    }


    protected int receiveByt()
    {
        var data = receiveBytes(4);
        return BitConverter.ToInt32(data);
    }

    protected string receiveString(int length)
    {
        var data = receiveBytes(length);
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