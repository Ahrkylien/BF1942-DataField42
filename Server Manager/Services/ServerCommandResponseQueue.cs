using System.Threading.Tasks;

public class ServerCommandResponseQueue
{
    private readonly SemaphoreSlim _serverCommandListenQueueRegistrationSemaphore = new(1, 1);

    private readonly List<Tuple<IEnumerable<ServerCommand>, TaskCompletionSource<Tuple<ServerCommand, byte[]>>>> _serverCommandListenQueue = new();

    private readonly List<Tuple<ServerCommand, byte[]>> _oldServerCommands = new();

    public async Task AwaitSemaphore()
    {
        await _serverCommandListenQueueRegistrationSemaphore.WaitAsync();
    }

    public void ReleaseSemaphore()
    {
        _serverCommandListenQueueRegistrationSemaphore.Release();
    }

    public TaskCompletionSource<Tuple<ServerCommand, byte[]>> Enqueue(IEnumerable<ServerCommand> responseCommands)
    {
        TaskCompletionSource<Tuple<ServerCommand, byte[]>> taskCompletionSource = new();
        _serverCommandListenQueue.Add(new(responseCommands, taskCompletionSource));
        _ = Task.Run(() => SetTimout(taskCompletionSource));
        return taskCompletionSource;
    }

    private async Task SetTimout(TaskCompletionSource<Tuple<ServerCommand, byte[]>> taskCompletionSource, TimeSpan? timeout = null)
    {
        await Task.Delay(timeout ?? TimeSpan.FromSeconds(2));
        if (!taskCompletionSource.Task.IsCompleted)
        {
            await AwaitSemaphore();
            for (int i = 0; i < _serverCommandListenQueue.Count; i++)
            {
                if (_serverCommandListenQueue[i].Item2 == taskCompletionSource)
                {
                    _serverCommandListenQueue.RemoveAt(i);
                    break;
                }
            }
            ReleaseSemaphore();
            taskCompletionSource.SetException(new TimeoutException($"The server did not respond within time."));
        }
    }

    public async Task SetResults(ServerCommand serverCommand, byte[] data)
    {
        await AwaitSemaphore();
        if (_serverCommandListenQueue.Count > 0)
        {
            (var awaitingResponses, var taskCompletionSource) = _serverCommandListenQueue.First();
            if (awaitingResponses.Contains(serverCommand))
            {
                taskCompletionSource.SetResult(new(serverCommand, data));
                _serverCommandListenQueue.RemoveAt(0);
            }
        }
        ReleaseSemaphore();
    }
}