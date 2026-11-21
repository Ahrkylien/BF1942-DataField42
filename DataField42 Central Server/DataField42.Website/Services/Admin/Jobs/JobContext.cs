namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// How a running job reports what it is doing. A job calls these as it works; the admin page
/// polls the result.
/// </summary>
public sealed class JobContext
{
    public const int LinesShownOnPage = 500;

    private readonly Lock _lock = new();
    private readonly List<string> _log = [];

    /// <summary>
    /// Arguments from the admin page, such as the mod to limit the run to.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    public string? Step { get; private set; }

    public int CompletedCount { get; private set; }

    /// <summary>
    /// How many items this run will process, once the job knows. Zero means it does not yet, and
    /// no percentage is reported.
    /// </summary>
    public int TotalCount { get; private set; }

    public double? Percentage => TotalCount == 0 ? null : Math.Min(100, (double)CompletedCount / TotalCount * 100);

    /// <summary>
    /// Names the phase the job has reached and resets the item count, so a job with several
    /// phases reports a percentage of the phase it is in.
    /// </summary>
    public void BeginStep(string step, int totalCount = 0)
    {
        using (_lock.EnterScope())
        {
            Step = step;
            TotalCount = totalCount;
            CompletedCount = 0;
        }

        Log(step);
    }

    public void SetTotalCount(int totalCount)
    {
        using (_lock.EnterScope())
            TotalCount = totalCount;
    }

    public void Advance(int count = 1)
    {
        using (_lock.EnterScope())
            CompletedCount += count;
    }

    /// <summary>
    /// Records a line in the log, stamped with the time.
    /// </summary>
    public void Log(string message)
    {
        using (_lock.EnterScope())
        {
            _log.Add($"{DateTime.Now:HH:mm:ss}  {message}");

        }
    }

    /// <summary>
    /// The end of the log, which is what the admin page shows.
    /// </summary>
    public List<string> SnapshotLog(int tail = LinesShownOnPage)
    {
        using (_lock.EnterScope())
            return [.. _log.Skip(Math.Max(0, _log.Count - tail))];
    }

    /// <summary>
    /// The whole log, as the text that is stored.
    /// </summary>
    public string FullLog()
    {
        using (_lock.EnterScope())
            return string.Join('\n', _log);
    }

    public string? GetParameter(string name) =>
        Parameters.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

    public int? GetIntParameter(string name) =>
        int.TryParse(GetParameter(name), out int value) ? value : null;
}
