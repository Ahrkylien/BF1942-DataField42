using System.Collections.Concurrent;
using DataField42.Website.Data.Entities;

namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Runs the admin jobs in the background and keeps their progress, so the admin page can start
/// one and then watch it without holding a request open. One run of a given job at a time: these
/// jobs write the same tables and reading the same archives twice over would only slow both runs
/// down.
///
/// Every run is also kept in the database by <see cref="JobRunStore"/>, so what a job did survives
/// a restart. A job that has not run since the process started reports its last stored run.
/// </summary>
public sealed class JobRunner(IServiceScopeFactory scopeFactory, JobRunStore store, ILogger<JobRunner> logger)
{
    /// <summary>
    /// How often a running job's progress and log are written to the database.
    /// </summary>
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, JobExecution> _executions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JobStatus> _lastStoredRuns = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Settles what the last shutdown left behind, and loads each job's most recent run so its
    /// card shows what it did before the restart. Called once, as the application starts.
    /// </summary>
    public async Task RestoreAsync()
    {
        await store.MarkInterruptedAsync();

        using var scope = scopeFactory.CreateScope();
        foreach (var job in scope.ServiceProvider.GetServices<IAdminJob>())
            if (await store.LatestAsync(job.JobId) is { } run)
                _lastStoredRuns[job.JobId] = ToStatus(job, run);
    }

    /// <summary>
    /// Starts a job, unless it is already running. Returns false when it is.
    /// </summary>
    public bool TryStart(string jobId, IReadOnlyDictionary<string, string> parameters, out string? error)
    {
        var existing = _executions.GetValueOrDefault(jobId);
        if (existing is { IsRunning: true })
        {
            error = "That job is already running.";
            return false;
        }

        // Resolving the job here, on the request's own scope, means an unknown id is reported
        // before anything is started.
        using var probeScope = scopeFactory.CreateScope();
        if (FindJob(probeScope.ServiceProvider, jobId) is null)
        {
            error = $"There is no job called '{jobId}'.";
            return false;
        }

        var execution = new JobExecution(jobId, parameters);
        _executions[jobId] = execution;
        execution.Task = Task.Run(() => RunAsync(execution));

        error = null;
        return true;
    }

    /// <summary>
    /// Asks a running job to stop. The job stops at its next item rather than immediately, so the
    /// database is never left half-written mid-item.
    /// </summary>
    public bool TryCancel(string jobId)
    {
        var execution = _executions.GetValueOrDefault(jobId);
        if (execution is not { IsRunning: true })
            return false;

        execution.Cancellation.Cancel();
        return true;
    }

    /// <summary>
    /// The current state of every job, whether it has ever run or not.
    /// </summary>
    public List<JobStatus> GetStatuses()
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetServices<IAdminJob>().ToList();

        return [.. jobs.Select(GetStatus)];
    }

    public JobStatus? GetStatus(string jobId)
    {
        using var scope = scopeFactory.CreateScope();
        var job = FindJob(scope.ServiceProvider, jobId);
        return job is null ? null : GetStatus(job);
    }

    /// <summary>
    /// A job's name, for pages about it, or <see langword="null"/> if there is no such job.
    /// </summary>
    public string? GetName(string jobId) => GetStatus(jobId)?.Name;

    private JobStatus GetStatus(IAdminJob job)
    {
        if (_executions.GetValueOrDefault(job.JobId) is { } execution)
            return ToStatus(job, execution);

        return _lastStoredRuns.GetValueOrDefault(job.JobId) ?? new JobStatus(
            job.JobId, job.Name, job.Description, JobState.Idle, null, null, 0, 0, null, null, null, []);
    }

    private static JobStatus ToStatus(IAdminJob job, JobExecution execution) => new(
        job.JobId,
        job.Name,
        job.Description,
        execution.State,
        execution.Context.Step,
        execution.Context.Percentage,
        execution.Context.CompletedCount,
        execution.Context.TotalCount,
        execution.StartedUtc,
        execution.FinishedUtc,
        execution.Error,
        execution.Context.SnapshotLog());

    private static JobStatus ToStatus(IAdminJob job, AdminJobRun run)
    {
        var state = Enum.TryParse<JobState>(run.State, out var parsed) ? parsed : JobState.Failed;
        double? percentage = run.TotalCount == 0 ? null : Math.Min(100, (double)run.CompletedCount / run.TotalCount * 100);

        return new JobStatus(
            job.JobId,
            job.Name,
            job.Description,
            state,
            run.Step,
            percentage,
            run.CompletedCount,
            run.TotalCount,
            JobRunStore.AsUtc(run.StartedUtc),
            run.FinishedUtc is { } finished ? JobRunStore.AsUtc(finished) : null,
            run.Error,
            run.Log.Length == 0 ? [] : [.. run.Log.Split('\n').TakeLast(JobContext.LinesShownOnPage)]);
    }

    private static IAdminJob? FindJob(IServiceProvider services, string jobId) =>
        services.GetServices<IAdminJob>().FirstOrDefault(job => string.Equals(job.JobId, jobId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Runs one job on a scope of its own: the request that started it is long gone by the time
    /// the job finishes, and so is that request's database context.
    /// </summary>
    private async Task RunAsync(JobExecution execution)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var job = FindJob(scope.ServiceProvider, execution.JobId);
        if (job is null)
        {
            execution.Fail("The job is no longer registered.");
            return;
        }

        logger.LogInformation("Admin job {JobId} started", execution.JobId);

        int? runId = await store.BeginAsync(execution.JobId, execution.StartedUtc, execution.Context.Parameters);
        using var stopSaving = new CancellationTokenSource();
        var saving = runId is { } id ? SavePeriodicallyAsync(execution, id, stopSaving.Token) : Task.CompletedTask;

        try
        {
            await job.RunAsync(execution.Context, execution.Cancellation.Token);
            execution.Succeed();
            logger.LogInformation("Admin job {JobId} finished", execution.JobId);
        }
        catch (OperationCanceledException)
        {
            execution.Cancel();
            logger.LogInformation("Admin job {JobId} was cancelled", execution.JobId);
        }
        catch (Exception ex)
        {
            execution.Fail(ex.Message);
            execution.Context.Log($"FAILED: {ex}");
            logger.LogError(ex, "Admin job {JobId} failed", execution.JobId);
        }
        finally
        {
            // Stopped before the last write, so a periodic save cannot land after it and put a
            // finished run back to running.
            await stopSaving.CancelAsync();
            await saving;

            if (runId is { } finishedId)
                await store.SaveAsync(finishedId, execution.Snapshot());
        }
    }

    private async Task SavePeriodicallyAsync(JobExecution execution, int runId, CancellationToken stop)
    {
        using var timer = new PeriodicTimer(SaveInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stop))
                await store.SaveAsync(runId, execution.Snapshot());
        }
        catch (OperationCanceledException)
        {
            // The job has ended; its final state is written by whoever stopped this.
        }
    }

    /// <summary>
    /// One run of one job.
    /// </summary>
    private sealed class JobExecution(string jobId, IReadOnlyDictionary<string, string> parameters)
    {
        public string JobId { get; } = jobId;

        public JobContext Context { get; } = new() { Parameters = parameters };

        public CancellationTokenSource Cancellation { get; } = new();

        public JobState State { get; private set; } = JobState.Running;

        public DateTime StartedUtc { get; } = DateTime.UtcNow;

        public DateTime? FinishedUtc { get; private set; }

        public string? Error { get; private set; }

        public Task? Task { get; set; }

        public bool IsRunning => State == JobState.Running;

        public void Succeed() => Finish(JobState.Succeeded, null);

        public void Cancel() => Finish(JobState.Cancelled, null);

        public void Fail(string error) => Finish(JobState.Failed, error);

        /// <summary>
        /// Where the run is now, in the form that is stored.
        /// </summary>
        public JobRunSnapshot Snapshot() => new(
            State,
            Context.Step,
            Context.CompletedCount,
            Context.TotalCount,
            FinishedUtc,
            Error,
            Context.FullLog());

        private void Finish(JobState state, string? error)
        {
            State = state;
            Error = error;
            FinishedUtc = DateTime.UtcNow;
        }
    }
}
