using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Keeps every run of every admin job in the database, so that what a job did outlives the process
/// that ran it: a restart or a reboot does not take the log with it.
///
/// Saving must never be what stops a job. A job that runs for an hour has nothing to gain from
/// being failed because the database blinked, so every write here logs a failure and carries on.
/// </summary>
public sealed class JobRunStore(IServiceScopeFactory scopeFactory, ILogger<JobRunStore> logger)
{
    /// <summary>
    /// What a run that was cut off reads as.
    /// </summary>
    public const string InterruptedMessage = "The server stopped while this job was running.";

    /// <summary>
    /// Records that a run has started, and returns its id, or <see langword="null"/> if it could
    /// not be recorded.
    /// </summary>
    public async Task<int?> BeginAsync(string jobId, DateTime startedUtc, IReadOnlyDictionary<string, string> parameters)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<DataField42DbContext>();

            var run = new AdminJobRun
            {
                JobId = jobId,
                State = nameof(JobState.Running),
                StartedUtc = startedUtc,
                Parameters = parameters.Count == 0 ? null : string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}")),
            };
            context.AdminJobRuns.Add(run);
            await context.SaveChangesAsync();

            return run.Id;
        }
        catch (Exception ex) when (DatabaseFailure.IsUnavailable(ex))
        {
            logger.LogWarning(ex, "Could not record the start of a {JobId} run; its log will not be kept", jobId);
            return null;
        }
    }

    /// <summary>
    /// Writes where a run has got to. Called while it runs, so that a crash loses seconds and not
    /// the run, and again when it ends.
    /// </summary>
    public async Task SaveAsync(int runId, JobRunSnapshot snapshot)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<DataField42DbContext>();

            string state = snapshot.State.ToString();
            string? step = Truncate(snapshot.Step, 255);
            string log = snapshot.Log;

            await context.AdminJobRuns
                .Where(run => run.Id == runId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(run => run.State, state)
                    .SetProperty(run => run.Step, step)
                    .SetProperty(run => run.CompletedCount, snapshot.CompletedCount)
                    .SetProperty(run => run.TotalCount, snapshot.TotalCount)
                    .SetProperty(run => run.FinishedUtc, snapshot.FinishedUtc)
                    .SetProperty(run => run.Error, snapshot.Error)
                    .SetProperty(run => run.Log, log));
        }
        catch (Exception ex) when (DatabaseFailure.IsUnavailable(ex))
        {
            logger.LogWarning(ex, "Could not save run {RunId}", runId);
        }
    }

    /// <summary>
    /// Marks every run still recorded as running as interrupted. Called once as the application
    /// starts, when there can be no such run: whatever was running it has gone.
    /// </summary>
    public async Task MarkInterruptedAsync()
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<DataField42DbContext>();

            var now = DateTime.UtcNow;
            string line = $"{DateTime.Now:HH:mm:ss}  {InterruptedMessage}";
            string running = nameof(JobState.Running);
            string interrupted = nameof(JobState.Interrupted);

            int marked = await context.AdminJobRuns
                .Where(run => run.State == running)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(run => run.State, interrupted)
                    .SetProperty(run => run.FinishedUtc, now)
                    .SetProperty(run => run.Error, InterruptedMessage)
                    .SetProperty(run => run.Log, run => run.Log == "" ? line : run.Log + "\n" + line));

            if (marked > 0)
                logger.LogWarning("{Runs} cut off by the last shutdown", TextFormatting.Count(marked, "admin job run was", "admin job runs were"));
        }
        catch (Exception ex) when (DatabaseFailure.IsUnavailable(ex))
        {
            logger.LogWarning(ex, "Could not mark interrupted job runs");
        }
    }

    /// <summary>
    /// The most recent run of a job, or <see langword="null"/> if it has never run.
    /// </summary>
    public async Task<AdminJobRun?> LatestAsync(string jobId)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<DataField42DbContext>();

            return await context.AdminJobRuns
                .AsNoTracking()
                .Where(run => run.JobId == jobId)
                .OrderByDescending(run => run.StartedUtc)
                .ThenByDescending(run => run.Id)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex) when (DatabaseFailure.IsUnavailable(ex))
        {
            logger.LogWarning(ex, "Could not read the last run of {JobId}", jobId);
            return null;
        }
    }

    /// <summary>
    /// The runs of a job, newest first, without their logs.
    /// </summary>
    public async Task<List<JobRunSummary>> ListAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataField42DbContext>();

        var runs = await context.AdminJobRuns
            .AsNoTracking()
            .Where(run => run.JobId == jobId)
            .OrderByDescending(run => run.StartedUtc)
            .ThenByDescending(run => run.Id)
            .Select(run => new
            {
                run.Id, run.State, run.StartedUtc, run.FinishedUtc, run.Parameters, run.CompletedCount, run.TotalCount, run.Error,
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. runs.Select(run => new JobRunSummary(
                run.Id,
                run.State,
                AsUtc(run.StartedUtc),
                run.FinishedUtc is { } finished ? AsUtc(finished) : null,
                run.Parameters,
                run.CompletedCount,
                run.TotalCount,
                run.Error))
        ];
    }

    public async Task<AdminJobRun?> FindAsync(int runId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataField42DbContext>();

        return await context.AdminJobRuns.AsNoTracking().FirstOrDefaultAsync(run => run.Id == runId, cancellationToken);
    }

    /// <summary>
    /// A time the database returned, which carries no kind, as the UTC it was stored as.
    /// </summary>
    public static DateTime AsUtc(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Utc);

    private static string? Truncate(string? text, int length) =>
        text is not null && text.Length > length ? text[..length] : text;
}
