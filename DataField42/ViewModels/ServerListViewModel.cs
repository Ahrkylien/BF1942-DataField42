using Microsoft.Extensions.Logging;

namespace DataField42.ViewModels;

public partial class ServerListViewModel : AbstractServerListViewModel
{
    public ServerListViewModel(
        MainWindowViewModel mainWindowViewModel,
        ILogger<AbstractServerListViewModel> logger,
        ILoggerFactory loggerFactory,
        Bf1942ServerLobby serverLobby)
        : base(mainWindowViewModel, logger, loggerFactory, serverLobby) { }

    protected override void ServerSelectedHandler(ServerViewModel serverViewModel)
    {
        _logger.LogDebug($"Server selected from list: {serverViewModel.Ip}:{serverViewModel.QueryPort}.");
        _mainWindowViewModel.GoToSyncMenu(serverViewModel);
    }
}
