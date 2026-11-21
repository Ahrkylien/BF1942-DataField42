using System.Diagnostics;

namespace DataField42.Website.Services.Admin.Update;

/// <summary>
/// Runs a program and collects what it printed. Arguments are passed as a list, never through a
/// shell, so a branch name cannot be anything but an argument.
/// </summary>
public static class ProcessRunner
{
    public sealed record Result(int ExitCode, string Output, string Error)
    {
        public bool Succeeded => ExitCode == 0;
    }

    public static async Task<Result> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = Start(fileName, arguments, workingDirectory);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        var output = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var error = process.StandardError.ReadToEndAsync(timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            return new Result(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            if (cancellationToken.IsCancellationRequested)
                throw;

            throw new TimeoutException($"{fileName} did not finish within {timeout.TotalSeconds:0} seconds.");
        }
    }

    /// <summary>
    /// Runs a program, handing every line it prints, from either output, to
    /// <paramref name="onLine"/> as it appears, and returns its exit code.
    /// </summary>
    public static async Task<int> RunLoggedAsync(
        string fileName,
        IEnumerable<string> arguments,
        string? workingDirectory,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        using var process = Start(fileName, arguments, workingDirectory);

        async Task PumpAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
                onLine(line);
        }

        await Task.WhenAll(PumpAsync(process.StandardOutput), PumpAsync(process.StandardError), process.WaitForExitAsync(cancellationToken));
        return process.ExitCode;
    }

    private static Process Start(string fileName, IEnumerable<string> arguments, string? workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        // git must fail rather than ask for credentials nobody can type; dotnet must not greet.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
    }
}
