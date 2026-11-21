using Microsoft.EntityFrameworkCore;
using DataField42.Website.Data;

namespace DataField42.Website.Services;

/// <summary>
/// Averages a server's player count over an average week, which is what makes a weekly rhythm
/// visible: the last several weeks are laid on top of each other and averaged point by point.
/// </summary>
public sealed class PlayerActivityStatistics(DataField42DbContext context)
{
    private const int SecondsPerWeek = 7 * 24 * 60 * 60;

    /// <summary>
    /// The average week for one server, sampled over the preceding
    /// <paramref name="weekCount"/> weeks and returned as points within the week that just
    /// ended.
    /// </summary>
    public async Task<IReadOnlyList<PlayerCountSample>> GetWeekAverageAsync(int serverId, int weekCount = 10, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var start = now.AddDays(-7 * weekCount);

        // The sample just before the window is included so the interpolation has a value to
        // start from rather than beginning with a gap.
        var preceding = await context.PlayerNumbers
            .Where(p => p.Date <= start && p.Round!.ServerId == serverId)
            .OrderByDescending(p => p.Date)
            .Take(1)
            .ToListAsync(cancellationToken);

        var samples = await context.PlayerNumbers
            .Where(p => p.Date > start && p.Date <= now && p.Round!.ServerId == serverId)
            .OrderBy(p => p.Date)
            .ToListAsync(cancellationToken);

        var series = preceding.Concat(samples)
            .Where(p => p.Date.HasValue && p.Players.HasValue)
            .Select(p => ((int)(p.Date!.Value - start).TotalSeconds, (double)p.Players!.Value))
            .ToList();

        if (series.Count == 0)
            return [];

        var averaged = AverageOverWeeks(series, weekCount);
        return [.. averaged.Select(point => new PlayerCountSample(now.AddSeconds(point.Second), (int)Math.Round(point.Players)))];
    }

    /// <summary>
    /// The raw series for one server over a window, used by the live graphs.
    /// </summary>
    public async Task<IReadOnlyList<PlayerCountSample>> GetSeriesAsync(int serverId, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var from = now - window;

        return await context.PlayerNumbers
            .Where(p => p.Date > from && p.Date <= now && p.Round!.ServerId == serverId)
            .OrderBy(p => p.Date)
            .Select(p => new PlayerCountSample(p.Date!.Value, p.Players ?? 0))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The total across every tracked server over a window. Samples with no round are the
    /// tracker's own whole-network counts.
    /// </summary>
    public async Task<IReadOnlyList<PlayerCountSample>> GetTotalSeriesAsync(TimeSpan window, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var from = now - window;

        return await context.PlayerNumbers
            .Where(p => p.Date > from && p.Date <= now && p.RoundId == null)
            .OrderBy(p => p.Date)
            .Select(p => new PlayerCountSample(p.Date!.Value, p.Players ?? 0))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Folds the series into one week: every sample time is reduced modulo a week to give the
    /// points to report on, each week is interpolated at those points, and the weeks are averaged
    /// ignoring the points a given week has no data for.
    /// </summary>
    private static List<(int Second, double Players)> AverageOverWeeks(List<(int Second, double Players)> series, int weekCount)
    {
        var seconds = new SortedSet<int> { 0, SecondsPerWeek };
        foreach (var (second, _) in series)
            if (second >= 0)
                seconds.Add(second % SecondsPerWeek);

        var points = seconds.ToList();
        var result = new List<(int, double)>(points.Count);

        foreach (int second in points)
        {
            double sum = 0;
            int count = 0;

            for (int week = 0; week < weekCount; week++)
                if (Interpolate(series, second + week * SecondsPerWeek) is { } value)
                {
                    sum += value;
                    count++;
                }

            result.Add((second, count == 0 ? 0 : sum / count));
        }

        return result;
    }

    /// <summary>
    /// Linear interpolation over the series, or <see langword="null"/> outside it -- a week the
    /// server was not yet tracked for contributes nothing rather than counting as zero players.
    /// </summary>
    private static double? Interpolate(List<(int Second, double Players)> series, int second)
    {
        if (second < series[0].Second || second > series[^1].Second)
            return null;

        int low = 0;
        int high = series.Count - 1;
        while (high - low > 1)
        {
            int middle = (low + high) / 2;
            if (series[middle].Second <= second)
                low = middle;
            else
                high = middle;
        }

        var (leftSecond, leftPlayers) = series[low];
        var (rightSecond, rightPlayers) = series[high];
        if (rightSecond == leftSecond)
            return leftPlayers;

        double fraction = (double)(second - leftSecond) / (rightSecond - leftSecond);
        return leftPlayers + fraction * (rightPlayers - leftPlayers);
    }
}
