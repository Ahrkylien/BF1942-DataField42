namespace DataField42.Website.Services.Tracking;

/// <summary>
/// What is worked out about a round once it has finished.
/// </summary>
public static class RoundStatistics
{
    /// <summary>
    /// The team that won: the one with more tickets left, and 0 for a draw.
    /// </summary>
    public static int WinningTeam(int tickets1, int tickets2) =>
        tickets1 > tickets2 ? 1 : tickets1 < tickets2 ? 2 : 0;

    /// <summary>
    /// The most players the round had, from what was sampled during it and the final status. When
    /// nothing was sampled during the round, the sample taken just before it stands in, since that
    /// is how many were there when it began.
    /// </summary>
    public static int MaximumPlayers(IReadOnlyCollection<int> duringRound, int? beforeRound, int atEnd)
    {
        int maximum = atEnd;

        if (duringRound.Count > 0)
            maximum = Math.Max(maximum, duringRound.Max());
        else if (beforeRound is { } before)
            maximum = Math.Max(maximum, before);

        return maximum;
    }

    /// <summary>
    /// The average number of players over the length of the round, weighting each sample by how long
    /// it held. A sample stands until the next one, and the last until the round ended; the one taken
    /// before the round stands from its start. <see langword="null"/> for a round with no length or
    /// no samples.
    /// </summary>
    public static double? AveragePlayers(DateTime start, DateTime end, int? beforeRound, IReadOnlyList<(DateTime Date, int Players)> duringRound)
    {
        if (end <= start)
            return null;

        int? current = beforeRound;
        DateTime cursor = start;
        double weightedSum = 0;
        double covered = 0;

        void Hold(DateTime until)
        {
            until = until > end ? end : until;
            if (current is { } players && until > cursor)
            {
                double seconds = (until - cursor).TotalSeconds;
                weightedSum += players * seconds;
                covered += seconds;
            }

            if (until > cursor)
                cursor = until;
        }

        foreach (var (date, players) in duringRound)
        {
            Hold(date);
            current = players;
        }

        Hold(end);

        return covered > 0 ? weightedSum / covered : null;
    }
}
