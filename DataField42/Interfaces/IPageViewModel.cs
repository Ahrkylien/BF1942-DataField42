namespace DataField42.Interfaces;

public interface IPageViewModel
{
    string Title { get; }
    Task EnterPage() => Task.CompletedTask;
    Task LeavePage() => Task.CompletedTask;
}