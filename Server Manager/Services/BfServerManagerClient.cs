using System.Text;

public class BfServerManagerClient
{
    private readonly BfServerManagerClientCommunication _communication;

    public Permissions Permissions => _communication.Permissions;

    public Settings Settings { get; set; }

    private readonly Dictionary<ServerCommand, TaskCompletionSource<byte[]>> _pendingRequests = [];

    /// <summary>
    /// Be sure to call Initialize before doing anything else
    /// </summary>
    /// <param name="serverIp"></param>
    /// <param name="serverPort"></param>
    /// <param name="username"></param>
    /// <param name="password"></param>
    public BfServerManagerClient(string serverIp, int serverPort, string username, string password)
    {
        _communication = new BfServerManagerClientCommunication(serverIp, serverPort, username, password);
        _communication.DataReceived += DataReceivedHandler;
        Settings = new(_communication);
    }

    public async Task Initialize(CancellationToken cancellationToken)
    {
        await _communication.Initialize(cancellationToken);
        Settings.Initialize(Permissions);
        await GetServerSettings(cancellationToken);
    }

    private void DataReceivedHandler(object? sender, Tuple<ServerCommand, byte[]> e)
    {
        var command = e.Item1;
        var data = e.Item2;
        
        switch (command)
        {
            case ServerCommand.Message:
                var message = Encoding.UTF8.GetString(data);
                Console.WriteLine($"Server send command: {command} {message}");
                break;
            case ServerCommand.MapChanged:
            case ServerCommand.SetNext:
            case ServerCommand.SetIdle:
                var map = data.Length == 1 && data[0] == 0 ? null : new Bf1942MapInstance(Encoding.UTF8.GetString(data));
                Console.WriteLine($"Server send command: {command} {map}");
                break;
            case ServerCommand.Kicked:
            case ServerCommand.Banned:
            case ServerCommand.Unbanned:
            case ServerCommand.NameAddedToBannedWords:
                message = Encoding.UTF8.GetString(data);
                Console.WriteLine($"Server send command: {command} {message}");
                break;
            default:
                if (data.Length == 0)
                    Console.WriteLine($"Server send command: {command}");
                else
                    Console.WriteLine($"Server send command: {command}, with {data.Length} bytes of data: {Encoding.UTF8.GetString(data)}");
                break;
        }
    }

    public async Task GetServerSettings(CancellationToken cancellationToken)
    {
        var fileContentsRaw = await _communication.ReceiveFile(FileAndCommands.ServerManager, cancellationToken);
        Settings.ParseSettingsFile(fileContentsRaw);
    }

    public async Task GetStatus(CancellationToken cancellationToken)
    {
        if (!Permissions.StatusPage)
            throw new PermissionException($"{nameof(GetStatus)} requires permission for {nameof(Permissions.StatusPage)}.");

        _communication.SendClientCommand(ClientCommand.GetStatus);
        Console.WriteLine($"{(await _communication.ReceiveStringWithLengthSpecified(cancellationToken))}");
    }

    public async Task GetChatCommands(CancellationToken cancellationToken)
    {
        var fileContentsRaw = await _communication.ReceiveFile(FileAndCommands.PlayerMenu, cancellationToken);
        var fileContents = Encoding.UTF8.GetString(fileContentsRaw);
        Console.WriteLine(fileContents);
    }

    public async Task GetServerLog(CancellationToken cancellationToken)
    {
        var fileContentsRaw = await _communication.ReceiveFile(FileAndCommands.ServerManagerLog, cancellationToken);
        var fileContents = Encoding.UTF8.GetString(fileContentsRaw);
        Console.WriteLine(fileContents);
    }

    public async Task GetPlayers(CancellationToken cancellationToken)
    {
        List<Bf1942Player> playerList = [];
        var fileContentsRaw = await _communication.ReceiveFile(FileAndCommands.PlayerList, cancellationToken);
        using MemoryStream memoryStream = new(fileContentsRaw);
        using StreamReader reader = new(memoryStream);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            string[] parts = line.Split(',');
            if (parts.Length == 10)
            {
                playerList.Add(new Bf1942Player(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], parts[7], parts[8], parts[9]));
            }
        }

        foreach (var player in playerList)
            Console.WriteLine(player);
    }

    public async Task GetClients(CancellationToken cancellationToken)
    {
        var fileContentsRaw = await _communication.ReceiveFile(FileAndCommands.ClientList, cancellationToken);
        var fileContents = Encoding.UTF8.GetString(fileContentsRaw);
        // fprintf(stream, "\"%s\",%i,%s,%s,%s\n", clientName, clientIdx, address, "Connected", lastActive);
        Console.WriteLine(fileContents);
    }

    public async Task GetUsers(CancellationToken cancellationToken)
    {
        var fileContentsRaw = await _communication.ReceiveFile(FileAndCommands.UserAccess, cancellationToken);
        var fileContents = Encoding.UTF8.GetString(fileContentsRaw);
        // fprintf(tmpStream, "%s,%s,%s,%s,%s\n", usernameTok, passwordTok, keyhashTok, accessTok, enabledTok);
        Console.WriteLine(fileContents);
    }

    public async Task GetAllMaps(CancellationToken cancellationToken)
    {
        List<Bf1942Map> mapList =[];
        var fileContentsRaw = await _communication.ReceiveFile(FileAndCommands.Maps, cancellationToken);
        using MemoryStream memoryStream = new(fileContentsRaw);
        using StreamReader reader = new(memoryStream);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            string[] parts = line.Split(',');
            if (parts.Length == 3)
            {
                mapList.Add(new Bf1942Map(parts[0], parts[1], parts[2]));
            }
        }

        foreach (var map in mapList)
            Console.WriteLine(map);
    }

    public async Task GetBanList(CancellationToken cancellationToken)
    {
        var fileContentsRaw = await _communication.ReceiveFile(FileAndCommands.ServerBanList, cancellationToken);
        var fileContents = Encoding.UTF8.GetString(fileContentsRaw);
        Console.WriteLine(fileContents);
    }

    /// <summary>
    /// Gray in-game global chat message
    /// </summary>
    /// <param name="message"></param>
    public void SayAll(string message)
    {
        _communication.SendClientCommand(ClientCommand.Chat, message);
    }

    /// <summary>
    /// Yellow in-game global message
    /// </summary>
    /// <param name="message"></param>
    public void SendServerMessage(string message)
    {
        _communication.SendClientCommand(ClientCommand.Message, message);
        // TODO: await ServerCommand.MessageSeen
    }

    public async Task SendConsoleCommand(string command, CancellationToken cancellationToken)
    {
        if (!Permissions.ConsoleCommands)
            throw new PermissionException($"{nameof(SendConsoleCommand)} requires permission for {nameof(Permissions.ConsoleCommands)}.");

        var response = await _communication.SendClientCommandAndAwaitServerResponse(
            ClientCommand.ConsoleCommand,
            command,
            new List<ServerCommand>() { ServerCommand.ConsoleCommandExecuted, ServerCommand.ErrorDuringRequest },
            cancellationToken);
        
        if (response.Item1 == ServerCommand.ErrorDuringRequest)
            throw new Exception($"The Server failed to execute the Console Command '{command}'");
    }

    public void ChangePassword(string newPassword)
    {
        _communication.SendClientCommand(ClientCommand.SetPassword, newPassword);
    }

    public async Task StartBf1942Server()
    {
        var tcs = new TaskCompletionSource<byte[]>();
        _pendingRequests.Add(ServerCommand.AlreadyRunning, tcs);
        _communication.SendClientCommand(ClientCommand.StartBf1942Server);
        var data = await tcs.Task;
        _pendingRequests.Remove(ServerCommand.AlreadyRunning);
    }

    public void StopBf1942Server()
    {
        _communication.SendClientCommand(ClientCommand.StopBf1942Server);
    }

    public void PauseGame()
    {
        _communication.SendClientCommand(ClientCommand.PauseGame);
    }

    public async Task<int> GetHostPort(CancellationToken cancellationToken)
    {
        _communication.SendClientCommand(ClientCommand.GetHostPort);
        return int.Parse(await _communication.ReceiveStringWithLengthSpecified(cancellationToken));
    }

    public void ChangeToMap(Bf1942MapInstance map)
    {
        _communication.SendClientCommand(ClientCommand.ChangeMap, map.ToString());
    }

    public void RunNextMap()
    {
        _communication.SendClientCommand(ClientCommand.RunNextMap);
    }

    public void SetNextMap(Bf1942MapInstance map)
    {
        _communication.SendClientCommand(ClientCommand.SetNextMap, map.ToString());
    }

    public void SetIdleMap(Bf1942MapInstance map)
    {
        _communication.SendClientCommand(ClientCommand.SetIdleMap, map.ToString());
    }

    public void RestartMap()
    {
        _communication.SendClientCommand(ClientCommand.RestartMap);
    }

    public void KickPlayerById(string id)
    {
        _communication.SendClientCommand(ClientCommand.Kick, id);
    }

    public void DisconnectClientById(string id)
    {
        _communication.SendClientCommand(ClientCommand.DisconnectClient, id);
    }

    public void UnbanPlayerById(string id)
    {
        _communication.SendClientCommand(ClientCommand.Unban, id);
    }

    public void BanPlayerById(string id)
    {
        _communication.SendClientCommand(ClientCommand.Ban, id);
    }

    public void BanPlayerByName(string name)
    {
        _communication.SendClientCommand(ClientCommand.BanName, name);
    }

    public void UnbanAllPlayers()
    {
        _communication.SendClientCommand(ClientCommand.UnbanAll);
    }

    public void UnbanPlayerByName(string name)
    {
        _communication.SendClientCommand(ClientCommand.Unban, name);
    }
}