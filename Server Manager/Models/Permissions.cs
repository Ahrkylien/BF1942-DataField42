using System.Text;

public class Permissions(UInt32 permissionsInt)
{
    /// <summary>
    /// Allowes setting certain settings via <see cref = "ClientCommand.SendServerManagerCon" />.
    /// </summary>
    public bool ServerPage { get; set; } = (permissionsInt & 1) != 0;

    /// <summary>
    /// Allowes setting certain settings via <see cref = "ClientCommand.SendServerManagerCon" />.
    /// </summary>
    public bool GamePage { get; set; } = (permissionsInt & 2) != 0;

    /// <summary>
    /// Allowes setting certain settings via <see cref = "ClientCommand.SendServerManagerCon" />.
    /// </summary>
    public bool FriendlyFirePage { get; set; } = (permissionsInt & 4) != 0;

    /// <summary>
    /// Allowes setting certain settings via <see cref = "ClientCommand.SendServerManagerCon" />.
    /// </summary>
    public bool MiscellaneousPage { get; set; } = (permissionsInt & 8) != 0;

    /// <summary>
    /// Allowes setting certain settings via <see cref = "ClientCommand.SendServerManagerCon" />.
    /// And allowes: <see cref = "ClientCommand.GetBannedWordsCon" />, <see cref = "ClientCommand.SendBannedWordsCon" />, <see cref = "ClientCommand.GetAnnouncementsCon" />, <see cref = "ClientCommand.SendAnnouncementsCon" />.
    /// </summary>
    public bool AdminPage { get; set; } = (permissionsInt & 16) != 0;

    /// <summary>
    /// Allowes: <see cref = "ClientCommand.GetStatus" />.
    /// </summary>
    public bool StatusPage { get; set; } = (permissionsInt & 32) != 0;

    /// <summary>
    /// Allowes: <see cref="ClientCommand.GetModsCon"/>, <see cref="ClientCommand.GetMapsCon"/>, <see cref="ClientCommand.GetServerMapListCon"/>, <see cref="ClientCommand.SendServerMaplistCon"/>, <see cref="ClientCommand.ChangeMap"/>, <see cref="ClientCommand.RunNextMap"/>, <see cref="ClientCommand.SetNextMap"/>, <see cref="ClientCommand.SetIdleMap"/>, <see cref="ClientCommand.RestartMap"/>.
    /// Also allowes these chat commands: !change, !restart, !setnext, !runnext
    /// </summary>
    public bool MapsPage { get; set; } = (permissionsInt & 64) != 0;

    /// <summary>
    /// Allowes: <see cref="ClientCommand.GetPlayerListCon"/>, <see cref="ClientCommand.GetPlayerMenuCon"/>, <see cref="ClientCommand.GetHostPort"/>, <see cref="ClientCommand.Kick"/>, <see cref="ClientCommand.BanName"/>, <see cref="ClientCommand.Screenshot"/>, <see cref="ClientCommand.Ban"/>, <see cref="ClientCommand.GetBannedWordsCon"/>, <see cref="ClientCommand.SendBannedWordsCon"/>.
    /// </summary>
    public bool PlayersPage { get; set; } = (permissionsInt & 128) != 0;

    /// <summary>
    /// Allowes: <see cref="ClientCommand.GetServerBanListCon"/>, <see cref="ClientCommand.SendServerBanListCon"/>, <see cref="ClientCommand.Unban"/>, <see cref="ClientCommand.UnbanAll"/>, <see cref="ClientCommand.Ban"/>.
    /// </summary>
    public bool BansPage { get; set; } = (permissionsInt & 256) != 0;

    /// <summary>
    /// Allowes: <see cref = "ClientCommand.GetUserAccessCon" />, <see cref = "ClientCommand.SendUserAccessCon" />.
    /// </summary>
    public bool UsersPage { get; set; } = (permissionsInt & 512) != 0;

    /// <summary>
    /// Allowes: <see cref = "ClientCommand.GetServerScheduleCon" />, <see cref = "ClientCommand.SendServerScheduleCon" />.
    /// </summary>
    public bool SchedulePage { get; set; } = (permissionsInt & 1024) != 0;

    /// <summary>
    /// Allowes: <see cref = "ClientCommand.GetClientListCon" />.
    /// </summary>
    public bool ClientsPage { get; set; } = (permissionsInt & 2048) != 0;

    /// <summary>
    /// Allowes: <see cref="ClientCommand.StartBf1942Server"/>, <see cref="ClientCommand.StopBf1942Server"/>, <see cref="ClientCommand.PauseGame"/>.
    /// </summary>
    public bool StartStopPauseServer { get; set; } = (permissionsInt & 4096) != 0;

    /// <summary>
    /// Allowes: <see cref="ClientCommand.GetServerManagerLog"/>.
    /// </summary>
    public bool ViewServerManagerLog { get; set; } = (permissionsInt & 8192) != 0;

    public bool LogonMulipleTimes { get; set; } = (permissionsInt & 16384) != 0;

    /// <summary>
    /// Allowes setting certain settings via <see cref = "ClientCommand.SendServerManagerCon" />.
    /// </summary>
    public bool CriticalSettings { get; set; } = (permissionsInt & 32768) != 0;

    /// <summary>
    /// Allowes: <see cref="ClientCommand.ConsoleCommand"/>.
    /// </summary>
    public bool ConsoleCommands { get; set; } = (permissionsInt & 65536) != 0;

    /// <summary>
    /// Allowes: <see cref="ClientCommand.GetPunkBusterServerConfig"/>, <see cref="ClientCommand.SendPunkBusterServerConfig"/>, <see cref="ClientCommand.PbDump"/>, <see cref="ClientCommand.PbLoad"/>.
    /// </summary>
    public bool EditPunkBusterSettings { get; set; } = (permissionsInt & 131072) != 0;

    /// <summary>
    /// For BF Vietnam.
    /// </summary>
    public bool EditCustomCombatSettings { get; set; } = (permissionsInt & 262144) != 0;

    /// <summary>
    /// Allowes: <see cref="ClientCommand.DisconnectClient"/>, <see cref="ClientCommand.SyncVerifyBanlist"/>, <see cref="ClientCommand.SyncToBanlist"/>, <see cref="ClientCommand.SyncFromBanlist"/>.
    /// </summary>
    public bool FullAdmin { get; set; } = permissionsInt == 0xFFFFFFFF;

    public override string ToString()
    {
        if (FullAdmin)
            return "Full Admin";
        
        StringBuilder stringBuilder = new();
        stringBuilder.Append(ServerPage ? 'S' : 's');
        stringBuilder.Append(GamePage ? 'G' : 'g');
        stringBuilder.Append(FriendlyFirePage ? 'F' : 'f');
        stringBuilder.Append(MiscellaneousPage ? 'I' : 'i');
        stringBuilder.Append(AdminPage ? 'A' : 'a');
        stringBuilder.Append(StatusPage ? 'T' : 't');
        stringBuilder.Append(MapsPage ? 'M' : 'h');
        stringBuilder.Append(PlayersPage ? 'P' : 'p');
        stringBuilder.Append(BansPage ? 'B' : 'b');
        stringBuilder.Append(UsersPage ? 'U' : 'u');
        stringBuilder.Append(SchedulePage ? 'H' : 'h');
        stringBuilder.Append(ClientsPage ? 'C' : 'c');
        stringBuilder.Append('-');
        stringBuilder.Append(StartStopPauseServer ? 'S' : 's');
        stringBuilder.Append(ViewServerManagerLog ? 'V' : 'v');
        stringBuilder.Append(LogonMulipleTimes ? 'M' : 'm');
        stringBuilder.Append(CriticalSettings ? 'R' : 'r');
        stringBuilder.Append(ConsoleCommands ? 'C' : 'c');
        stringBuilder.Append(EditPunkBusterSettings ? 'P' : 'p');
        stringBuilder.Append(EditCustomCombatSettings ? 'U' : 'u');
        return stringBuilder.ToString();
    }
}