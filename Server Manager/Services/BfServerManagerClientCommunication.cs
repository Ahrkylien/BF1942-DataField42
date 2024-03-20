using lzo.net;
using System.IO.Compression;
using System.Threading;

public class BfServerManagerClientCommunication(string serverIp, int serverPort, string username, string password) : TcpClientBase(serverIp, serverPort)
{
    private readonly string _username = username;

    private readonly string _password = password;

    public Permissions Permissions { get; private set; }

    public byte Version { get; private set; }

    public bool ServerRunning { get; private set; }

    public event EventHandler<Tuple<ServerCommand, byte[]>>? DataReceived;

    private readonly SemaphoreSlim _receiveSemaphore = new(1, 1);

    private readonly ServerCommandResponseQueue _serverCommandResponseQueue = new();

    public async Task Initialize(CancellationToken cancellationToken)
    {
        await Authenticate(cancellationToken);
        StartPolling();
    }

    public async Task<byte[]> ReceiveFile(IBfServerManagerFileReceivable serverManagerFile, CancellationToken cancellationToken)
    {
        bool semaphoreReleased = false;
        await _receiveSemaphore.WaitAsync(cancellationToken);
        try
        {
            SendClientCommand(serverManagerFile.ReceiveCommand);
            var size = await ReceiveStartOfNonServerCommandPacket(cancellationToken);

            if (size == 0xBEEFBABE)
                throw new Exception(); // TODO: better exception

            if (size == 0)
            {
                _receiveSemaphore.Release();
                return [];
            }

            var compressedLength = await ReceiveUInt32(cancellationToken: cancellationToken);
            var checksum = await ReceiveUInt32(cancellationToken: cancellationToken);
            var compressedDataBuffer = await ReceiveBytes((int)compressedLength, cancellationToken: cancellationToken);
            _receiveSemaphore.Release();
            semaphoreReleased = true;

            using var compressed = new MemoryStream(compressedDataBuffer);
            using var decompressed = new LzoStream(compressed, CompressionMode.Decompress);
            var buffer = new byte[size];
            decompressed.Read(buffer, 0, (int)size);

            // TODO: check checksum

            return buffer;
        }
        finally
        {
            if (!semaphoreReleased)
                _receiveSemaphore.Release();
        }
    }

    public void SendClientCommand(ClientCommand command, string? data = null)
    {
        SendUInt32(((UInt32)command + 0xbabe0000));
        if (data != null)
        SendString(data);
    }

    public async Task<Tuple<ServerCommand, byte[]>> SendClientCommandAndAwaitServerResponse(ClientCommand command, string? data, IEnumerable<ServerCommand> responseCommands, CancellationToken cancellationToken)
    {
        await _serverCommandResponseQueue.AwaitSemaphore();
        var taskCompletionSource = _serverCommandResponseQueue.Enqueue(responseCommands);
        SendClientCommand(command, data);
        _serverCommandResponseQueue.ReleaseSemaphore();
        await taskCompletionSource.Task;
        return taskCompletionSource.Task.Result;
    }

    public async Task<string> ReceiveStringWithLengthSpecified(CancellationToken cancellationToken)
    {
        await _receiveSemaphore.WaitAsync(cancellationToken);
        var length = await ReceiveStartOfNonServerCommandPacket(cancellationToken);
        var value = await ReceiveString((int)length, cancellationToken);
        _receiveSemaphore.Release();
        return value;
    }

    private async Task Authenticate(CancellationToken cancellationToken)
    {
        SendBytes(BfServerManagerEncryption.Encrypt(_username)); // 32 bytes
        SendBytes(BfServerManagerEncryption.Encrypt(_password)); // 32 bytes
        var returnCode = (await ReceiveBytes(1, cancellationToken: cancellationToken))[0];
        if (returnCode == 1)
            throw new Exception($"Can't authenticate bacause the username is incorrect ({_username}).");
        else if (returnCode == 2)
            throw new Exception("Can't authenticate bacause the password is incorrect.");
        else if (returnCode == 3)
            throw new Exception("Can't authenticate bacause the account is not enabled/activated.");
        else if (returnCode == 4)
            throw new Exception("Can't authenticate because user has no permissions.");
        else if (returnCode == 5)
            throw new Exception("Can't authenticate. No remote access is permitted.");
        else if(returnCode == 6)
            throw new Exception("Can't authenticate because user is already logged in.");
        else if (returnCode == 7)
            throw new Exception("Can't authenticate. Maximum number of connected clients has been reached.");
        else if (returnCode != 0)
            throw new Exception($"Can't authenticate, server responded with {returnCode}");
        Permissions = new Permissions(await ReceiveUInt32(cancellationToken: cancellationToken));
        Version = (await ReceiveBytes(1, cancellationToken: cancellationToken))[0];
        if (Version != 36 && Version != 37)
            throw new Exception($"Server has different Version: {Version}");
        SendByte(Version);
        ServerRunning = (await ReceiveBytes(1, cancellationToken: cancellationToken))[0] == 1;
        Console.WriteLine($"user permissions: {Permissions}");
        Console.WriteLine($"version: {Version}");
        Console.WriteLine($"server running: {ServerRunning}");
    }

    private void StartPolling()
    {
        // TODO: propper dispose via ct
        _ = Task.Run(() => ReceiveData(CancellationToken.None));
    }

    private async Task ReceiveData(CancellationToken cancellationToken)
    {
        int bytesRead;
        var buffer = new byte[4];
        while (true)
        {
            bytesRead = 0;
            while (bytesRead < 4)
            {
                if (bytesRead == 0)
                    await _receiveSemaphore.WaitAsync(cancellationToken);
                if (_stream.DataAvailable)
                    bytesRead += await _stream.ReadAsync(buffer.AsMemory(bytesRead, 4 - bytesRead), cancellationToken);
                if (bytesRead == 0)
                    _receiveSemaphore.Release();

            }
            var commandValueRaw = BitConverter.ToUInt32(buffer);
            var commandValue = (UInt16)(commandValueRaw - 0xBEEF0000);
            if (!Enum.IsDefined(typeof(ServerCommand), commandValue))
                throw new Exception($"Server has send unknown Command: 0x{commandValueRaw:X}");

            var serverCommand = (ServerCommand)commandValue;
            await FinnishReceivingServerCommand(serverCommand, cancellationToken);
            _receiveSemaphore.Release();
        }
    }

    private async Task<UInt32> ReceiveStartOfNonServerCommandPacket(CancellationToken cancellationToken, bool isFile = false)
    {
        UInt32 first4Bytes;
        while (true)
        {
            first4Bytes = await ReceiveUInt32(cancellationToken: cancellationToken);
            if (isFile && first4Bytes == 0xBEEFBABE)
                break;
            else if (first4Bytes >> 16 == 0xBEEF)
            {
                var commandValue = (UInt16)(first4Bytes - 0xBEEF0000);
                if (!Enum.IsDefined(typeof(ServerCommand), commandValue))
                    break;

                await FinnishReceivingServerCommand((ServerCommand)commandValue, cancellationToken);
            }
            else
                break;
        }
        return first4Bytes;
    }

    private async Task FinnishReceivingServerCommand(ServerCommand serverCommand, CancellationToken cancellationToken)
    {
        var dataLength = await ReceiveUInt32(cancellationToken: cancellationToken);
        var data = await ReceiveBytes((int)dataLength, cancellationToken: cancellationToken);
        PublishServerCommand(serverCommand, data);
    }

    /// <summary>
    /// This method will publish the event on its own thread
    /// </summary>
    /// <param name="serverCommand"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    private void PublishServerCommand(ServerCommand serverCommand, byte[] data)
    {
        _ = Task.Run(() => _serverCommandResponseQueue.SetResults(serverCommand, data));
        _ = Task.Run(() => DataReceived?.Invoke(this, new Tuple<ServerCommand, byte[]>(serverCommand, data)));
    }

    private new void SendString(string data)
    {
        SendUInt32((UInt32)data.Length + 1);
        base.SendString(data);
        SendByte(0);
    }
}

