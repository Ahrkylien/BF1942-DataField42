using Microsoft.Extensions.Logging;

namespace DataField42.ViewModels;

public partial class ServerSelectionViewModel : AbstractServerListViewModel
{
    public override string Title => "Select Server";

    private readonly TaskCompletionSource<ServerViewModel?> _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ServerSelectionViewModel(
        MainWindowViewModel mainWindowViewModel,
        ILogger<AbstractServerListViewModel> logger,
        ILoggerFactory loggerFactory,
        Bf1942ServerLobby serverLobby)
        : base(mainWindowViewModel, logger, loggerFactory, serverLobby) { }

    public Task<ServerViewModel?> AwaitSelection() => _taskCompletionSource.Task;

    public override Task LeavePage()
    {
        _logger.LogDebug("ServerSelectionViewModel leaving page — no selection made.");
        _taskCompletionSource.TrySetResult(null);
        return Task.CompletedTask;
    }

    protected override void ServerSelectedHandler(ServerViewModel serverViewModel)
    {
        _logger.LogDebug($"Server selected for popup: {serverViewModel.Ip}:{serverViewModel.QueryPort}.");
        _taskCompletionSource.TrySetResult(serverViewModel);
        _mainWindowViewModel.ClosePopUpCommand.Execute(null);
    }
}
