using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.IO;
using System.Threading;

namespace DataField42_Updater;
public partial class MainWindowViewModel : ObservableObject
{
    private const string clientExeName = "DataField42.exe";

    [ObservableProperty]
    private bool _showPopup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessages))]
    [NotifyPropertyChangedFor(nameof(HasMessagesOrErrors))]
    private string _messages = string.Empty;

    public bool HasMessages => !string.IsNullOrEmpty(Messages);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessages))]
    [NotifyPropertyChangedFor(nameof(HasMessagesOrErrors))]
    private string _errorMessages;

    public bool HasErrorMessages => !string.IsNullOrEmpty(ErrorMessages);

    public bool HasMessagesOrErrors => HasMessages || HasErrorMessages;

    [ObservableProperty]
    private int _percentage;

    public MainWindowViewModel()
    {
        Task.Run(async () => Initialize());
        // Environment.GetCommandLineArgs()
    }

    private async Task Initialize()
    {
        try
        {
            var communication = new DataField42Communication();
            var backgroundWorker = new DownloadBackgroundWorker();
            backgroundWorker.ProgressChanged += BackgroundWorkerCurrentFile_ProgressChanged;

            communication.StartSession();
            communication.SendString($"updateFile {clientExeName}");
            var fileSize = await communication.ReceiveUlong();
            backgroundWorker.TotalSize = fileSize;

            // TODO: check file size
            communication.SendAcknowledgement();

            using (var fileStream = new FileStream(clientExeName, FileMode.Create, FileAccess.Write))
            {
                await communication.ReceiveFile(fileSize, fileStream, backgroundWorker, CancellationToken.None);
            }
            communication.SendAcknowledgement();

            ExternalProcess.SwitchTo(clientExeName, arguments: CommandLineArguments.RawString);
        }
        catch (Exception ex)
        {
            DisplayError($"Can't download DataField42.exe: {ex.Message}");
        }
    }

    public void DisplayMessage(string message)
    {
        message = ">> " + message;
        if (HasMessages)
            message = $"\n{message}";
        Messages += message;
    }

    public void DisplayError(string message)
    {
        message = ">> " + message;
        if (HasErrorMessages)
            message = $"\n{message}";
        ErrorMessages += message;
    }

    private void BackgroundWorkerCurrentFile_ProgressChanged(int percentage)
    {
        Percentage = percentage;
    }
}
