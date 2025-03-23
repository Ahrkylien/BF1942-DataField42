namespace DataField42.ViewModels;

public partial class ServerSelectionViewModel : AbstractServerListViewModel
{
    public override string Title => "Select Server";

    private readonly TaskCompletionSource<ServerViewModel?> _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ServerSelectionViewModel(MainWindowViewModel mainWindowViewModel) : base(mainWindowViewModel) { }

    public Task<ServerViewModel?> AwaitSelection() => _taskCompletionSource.Task;

    public override Task LeavePage()
    {
        _taskCompletionSource.TrySetResult(null);
        return Task.CompletedTask;
    }

    protected override void ServerSelectedHandler(ServerViewModel serverViewModel)
    {
        _taskCompletionSource.TrySetResult(serverViewModel);
        _mainWindowViewModel.ClosePopUpCommand.Execute(null);
    }
}
