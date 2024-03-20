public static class FileAndCommands
{
    public static readonly BfServerManagerFileReceivableAndSendable ServerManager = new(ClientCommand.GetServerManagerCon, ClientCommand.SendServerManagerCon);
    public static readonly BfServerManagerFileReceivableAndSendable PunkBusterServer = new(ClientCommand.GetPunkBusterServerConfig, ClientCommand.SendPunkBusterServerConfig);
    public static readonly BfServerManagerFileReceivable Mods = new(ClientCommand.GetModsCon);
    public static readonly BfServerManagerFileReceivable Maps = new(ClientCommand.GetMapsCon);
    public static readonly BfServerManagerFileReceivableAndSendable ServerMapList = new(ClientCommand.GetServerMapListCon, ClientCommand.SendServerMaplistCon);
    public static readonly BfServerManagerFileReceivable PlayerList = new(ClientCommand.GetPlayerListCon);
    public static readonly BfServerManagerFileReceivable ClientList = new(ClientCommand.GetClientListCon);
    public static readonly BfServerManagerFileReceivable PlayerMenu = new(ClientCommand.GetPlayerMenuCon);
    public static readonly BfServerManagerFileReceivableAndSendable ServerBanList = new(ClientCommand.GetServerBanListCon, ClientCommand.SendServerBanListCon);
    public static readonly BfServerManagerFileReceivableAndSendable UserAccess = new(ClientCommand.GetUserAccessCon, ClientCommand.SendUserAccessCon);
    public static readonly BfServerManagerFileReceivableAndSendable ServerSchedule = new(ClientCommand.GetServerScheduleCon, ClientCommand.SendServerScheduleCon);
    public static readonly BfServerManagerFileReceivableAndSendable Announcements = new(ClientCommand.GetAnnouncementsCon, ClientCommand.SendAnnouncementsCon);
    public static readonly BfServerManagerFileReceivableAndSendable BannedWords = new(ClientCommand.GetBannedWordsCon, ClientCommand.SendBannedWordsCon);
    public static readonly BfServerManagerFileReceivable ServerManagerLog = new(ClientCommand.GetServerManagerLog);
}

public interface IBfServerManagerFileReceivable
{
    public ClientCommand ReceiveCommand { get; }
}

public interface IBfServerManagerFileSendable
{
    public ClientCommand ReceiveCommand { get; }
}
public class BfServerManagerFileReceivable(ClientCommand receiveCommand) : IBfServerManagerFileReceivable
{
    public ClientCommand ReceiveCommand { get; } = receiveCommand;
}

public class BfServerManagerFileReceivableAndSendable(ClientCommand receiveCommand, ClientCommand sendCommand) : BfServerManagerFileReceivable(receiveCommand), IBfServerManagerFileSendable
{
    public ClientCommand SendCommand { get; } = sendCommand;
}


