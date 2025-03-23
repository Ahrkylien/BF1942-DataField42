namespace DataField42.ViewModels;
public partial class ServerListViewModel : AbstractServerListViewModel
{
    public ServerListViewModel(MainWindowViewModel mainWindowViewModel) : base(mainWindowViewModel) { }

    protected override void ServerSelectedHandler(ServerViewModel serverViewModel)
    {
        _mainWindowViewModel.GoToSyncMenu(serverViewModel);
    }
}
