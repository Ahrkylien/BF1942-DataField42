namespace DataField42.Website.Services.Tracking;

/// <summary>
/// Looks at the master server's list on a schedule and has <see cref="TrackingRecorder"/> write what
/// it finds into the history. A failure only costs that pass: the next one tries again.
/// </summary>
public sealed class ServerTrackerService(
    IServiceScopeFactory scopeFactory,
    TrackerFeedReader feed,
    ILogger<ServerTrackerService> logger) : BackgroundService
{
    /// <summary>
    /// How long to wait between looks at the master server. It publishes new data about this often,
    /// so looking more often only repeats what was recorded.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long to wait before trying again after the list could not be read or a pass failed.
    /// </summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("The servers tracker is looking at the master server every {Interval}", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = Interval;

            try
            {
                if (await feed.ReadAsync(stoppingToken) is { } servers)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<TrackingRecorder>().RecordAsync(servers, stoppingToken);
                }
                else
                {
                    wait = RetryInterval;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "A tracker pass failed; trying again in {Wait}", RetryInterval);
                wait = RetryInterval;
            }

            try
            {
                await Task.Delay(wait, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
