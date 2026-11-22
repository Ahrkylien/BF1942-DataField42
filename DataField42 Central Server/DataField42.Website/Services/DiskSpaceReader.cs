using System.Diagnostics;
namespace DataField42.Website.Services;

/// <summary>
/// Reads filesystem usage from <c>df</c>. The storage this reports on includes a network share,
/// which the .NET drive APIs do not see, so the system tool is asked instead.
/// </summary>
public sealed class DiskSpaceReader(ILogger<DiskSpaceReader> logger)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Usage for each filesystem in <paramref name="names"/>, matched against either the device
    /// name or the mount point. A filesystem that is not mounted is left out.
    /// </summary>
    public async Task<IReadOnlyList<DiskSpace>> ReadAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        var wanted = names.ToList();
        if (wanted.Count == 0)
            return [];

        var rows = await ReadDfAsync(cancellationToken);
        var result = new List<DiskSpace>();

        foreach (var name in wanted)
            foreach (var row in rows)
            {
                var columns = row.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 6)
                    continue;
                if (columns[0] != name && columns[^1] != name)
                    continue;
                if (!long.TryParse(columns[1], out long total) || !long.TryParse(columns[2], out long used))
                    continue;

                result.Add(new DiskSpace { Name = name, TotalSize = total, UsedSize = used });
                break;
            }

        return result;
    }

    private async Task<IReadOnlyList<string>> ReadDfAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("df", "-P -k")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process is null)
                return [];

            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            await process.WaitForExitAsync(timeout.Token);

            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Could not read filesystem usage; df is unavailable on this host");
            return [];
        }
    }
}
