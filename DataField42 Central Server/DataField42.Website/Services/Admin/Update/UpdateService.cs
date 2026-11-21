using System.Globalization;
using System.Reflection;
using DataField42.Website.Configuration;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Services.Admin.Update;

/// <summary>
/// Updates the site from GitHub, from the admin panel: fetches a branch, builds it into a folder of
/// its own, and only if the build succeeds copies the result over the live site and restarts.
/// </summary>
/// <remarks>
/// The restart is the host's: this process stops itself, and the service manager that runs the site
/// starts it again from the new files. Files are replaced by writing a temporary file and renaming it
/// over the old one, so a file the running process has open is never rewritten in place.
/// </remarks>
public sealed class UpdateService(
    IOptions<WebsiteOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<UpdateService> logger)
{
    private const string ProjectPath = "DataField42 Central Server/DataField42.Website/DataField42.Website.csproj";

    /// <summary>
    /// Written by the "Analyse maps" job into the live site, so an update must never delete them.
    /// </summary>
    private const string GeneratedDirectory = "wwwroot/images/ingamemaps/";

    private const int CommitsShown = 100;
    private const int CommitsShownWithoutBaseline = 20;
    private const int MaxLogBytes = 512 * 1024;

    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(20);

    private readonly Lock _gate = new();
    private readonly Lock _logLock = new();
    private bool _running;

    private UpdateOptions Settings => options.Value.Update;

    /// <summary>
    /// Whether this installation can update itself: only a server set up by the install script has
    /// the folders configured.
    /// </summary>
    public bool IsAvailable => Settings.IsConfigured;

    /// <summary>
    /// The commit the running site was built from, and the branch when the last update or install
    /// recorded it.
    /// </summary>
    public DeployedVersion? GetDeployed()
    {
        string? commit = null;
        string? branch = null;

        if (Settings.StateDirectory is { Length: > 0 } stateDirectory && File.Exists(Path.Combine(stateDirectory, "deployed")))
        {
            foreach (string line in File.ReadAllLines(Path.Combine(stateDirectory, "deployed")))
            {
                if (line.StartsWith("commit=", StringComparison.Ordinal))
                    commit = line["commit=".Length..].Trim();
                else if (line.StartsWith("branch=", StringComparison.Ordinal))
                    branch = line["branch=".Length..].Trim();
            }
        }

        // A build made by hand still carries the commit it was built from.
        commit ??= CommitFromAssembly();

        return string.IsNullOrEmpty(commit) ? null : new DeployedVersion(string.IsNullOrEmpty(branch) ? null : branch, commit);
    }

    /// <summary>
    /// The state of the last update, which is what a person watching the page needs: whether one is
    /// running, how the last one ended, and its log.
    /// </summary>
    public UpdateStatus GetStatus()
    {
        var (state, changedUtc) = ReadState();
        return new UpdateStatus(state, changedUtc, ReadLog(), GetDeployed());
    }

    /// <summary>
    /// The branches the clone knows of, as of its last fetch.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetBranchesAsync(CancellationToken cancellationToken)
    {
        var result = await GitAsync(["for-each-ref", "--format=%(refname:strip=3)", "refs/remotes/origin"], QuickTimeout, cancellationToken);
        if (!result.Succeeded)
            return [];

        return [.. result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => name != "HEAD")
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Fetches the branch from GitHub and lists what it holds that the running site does not.
    /// </summary>
    public async Task<UpdateCheck> CheckAsync(string branch, CancellationToken cancellationToken)
    {
        if (_running)
            throw new UpdateException("An update is running; check again when it has finished.");

        var fetch = await GitAsync(["fetch", "--quiet", "--prune", "origin"], GitTimeout, cancellationToken);
        if (!fetch.Succeeded)
            throw new UpdateException($"Could not fetch from GitHub: {fetch.Error.Trim()}");

        if (!(await GetBranchesAsync(cancellationToken)).Contains(branch))
            throw new UpdateException($"There is no branch '{branch}' on GitHub.");

        string reference = $"refs/remotes/origin/{branch}";
        string tip = await RequireOutputAsync(["rev-parse", reference], cancellationToken);
        var deployed = GetDeployed();

        bool baselineKnown = deployed is not null
            && (await GitAsync(["cat-file", "-e", $"{deployed.Commit}^{{commit}}"], QuickTimeout, cancellationToken)).Succeeded;

        string range = baselineKnown ? $"{deployed!.Commit}..{reference}" : reference;
        int count = baselineKnown ? CommitsShown : CommitsShownWithoutBaseline;
        string log = await RequireOutputAsync(
            ["log", $"--max-count={count}", "--format=%H%x1f%an%x1f%aI%x1f%s", range, "--"], cancellationToken);

        return new UpdateCheck(
            branch,
            tip,
            deployed is not null && string.Equals(deployed.Commit, tip, StringComparison.OrdinalIgnoreCase),
            [.. log.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(ParseCommit)],
            !baselineKnown);
    }

    /// <summary>
    /// Starts an update to the branch and returns at once; it runs in the background and ends by
    /// restarting the site. Only one update runs at a time.
    /// </summary>
    public async Task StartAsync(string branch, CancellationToken cancellationToken)
    {
        if (!(await GetBranchesAsync(cancellationToken)).Contains(branch))
            throw new UpdateException($"There is no branch '{branch}'. Check for updates first.");

        using (_gate.EnterScope())
        {
            if (_running)
                throw new UpdateException("An update is running already.");

            _running = true;
        }

        // Not tied to the request: the update outlives the click that started it.
        _ = Task.Run(() => RunUpdateAsync(branch));
    }

    private async Task RunUpdateAsync(string branch)
    {
        bool deployed = false;

        try
        {
            Directory.CreateDirectory(Settings.StateDirectory!);
            File.WriteAllText(LogPath, string.Empty);
            WriteState("running");
            Log($"Updating to {branch}");

            string commit = await FetchAndCheckOutAsync(branch);
            await BuildAsync(commit);

            Log("Copying the build over the running site");
            Mirror(Settings.BuildDirectory!, Settings.TargetDirectory!);
            deployed = true;

            File.WriteAllText(Path.Combine(Settings.StateDirectory!, "deployed"), $"branch={branch}\ncommit={commit}\n");
            Log($"The site now runs {branch} at {commit}. Restarting.");
            WriteState("succeeded");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The update to {Branch} failed", branch);
            Log($"Error: {ex.Message}");
            Log(deployed
                ? "The files were already copied, so the site restarts to run them."
                : "The running site was not changed.");
            WriteState("failed");
        }
        finally
        {
            _running = false;
        }

        // The service manager starts the site again, from the files just written.
        if (deployed)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            lifetime.StopApplication();
        }
    }

    private async Task<string> FetchAndCheckOutAsync(string branch)
    {
        Log("Fetching from GitHub");
        string source = Settings.SourceDirectory!;

        await RunLoggedAsync("git", ["-C", source, "fetch", "--quiet", "--prune", "origin"]);
        await RunLoggedAsync("git", ["-C", source, "checkout", "--quiet", "--force", "-B", branch, $"refs/remotes/origin/{branch}"]);

        string commit = await RequireOutputAsync(["rev-parse", "HEAD"], CancellationToken.None);
        Log(await RequireOutputAsync(["log", "-1", "--format=%h %s"], CancellationToken.None));
        return commit;
    }

    private async Task BuildAsync(string commit)
    {
        Log("Building");
        string build = Settings.BuildDirectory!;

        // Into a folder of its own: a build that fails leaves the running site as it was.
        Directory.CreateDirectory(build);
        foreach (string entry in Directory.EnumerateFileSystemEntries(build))
        {
            if (Directory.Exists(entry))
                Directory.Delete(entry, recursive: true);
            else
                File.Delete(entry);
        }

        await RunLoggedAsync(
            "dotnet",
            ["publish", Path.Combine(Settings.SourceDirectory!, ProjectPath), "-c", "Release", "-o", build, $"-p:SourceRevisionId={commit}", "--nologo", "-v", "quiet"]);
        Log("Build succeeded.");
    }

    /// <summary>
    /// Makes <paramref name="target"/> match <paramref name="source"/>, except for the images the
    /// site generates, which are never deleted.
    /// </summary>
    private static void Mirror(string source, string target)
    {
        var wanted = new HashSet<string>(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            wanted.Add(Normalize(relative));

            string destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            // Written beside and renamed into place: a running process that has the old file open or
            // mapped keeps the old one, where overwriting it in place could crash that process.
            string temporary = destination + ".update";
            File.Copy(file, temporary, overwrite: true);
            File.Move(temporary, destination, overwrite: true);
        }

        foreach (string file in Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories).ToList())
        {
            string relative = Normalize(Path.GetRelativePath(target, file));
            if (!wanted.Contains(relative) && !relative.StartsWith(GeneratedDirectory, StringComparison.Ordinal))
                File.Delete(file);
        }

        // Folders the build no longer has, now empty.
        foreach (string directory in Directory.EnumerateDirectories(target, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
    }

    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/');

    private (UpdateState State, DateTime? ChangedUtc) ReadState()
    {
        if (!IsAvailable)
            return (UpdateState.Idle, null);

        string statusPath = Path.Combine(Settings.StateDirectory!, "status");
        UpdateState recorded = UpdateState.Idle;
        DateTime? changedUtc = null;

        if (File.Exists(statusPath))
        {
            string[] parts = File.ReadAllText(statusPath).Trim().Split(' ');
            recorded = parts[0] switch
            {
                "running" => UpdateState.Running,
                "succeeded" => UpdateState.Succeeded,
                "failed" => UpdateState.Failed,
                _ => UpdateState.Idle,
            };

            if (parts.Length > 1 && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
                changedUtc = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }

        if (_running)
            return (UpdateState.Running, changedUtc);

        // Recorded as running, but nothing is: the site was stopped in the middle of an update.
        return (recorded == UpdateState.Running ? UpdateState.Interrupted : recorded, changedUtc);
    }

    private void WriteState(string state)
    {
        string path = Path.Combine(Settings.StateDirectory!, "status");
        File.WriteAllText(path, $"{state} {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n");
    }

    private string LogPath => Path.Combine(Settings.StateDirectory!, "update.log");

    private void Log(string message)
    {
        using (_logLock.EnterScope())
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  {message}\n");
    }

    private string ReadLog()
    {
        if (!IsAvailable || !File.Exists(LogPath))
            return string.Empty;

        using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        bool cut = stream.Length > MaxLogBytes;
        if (cut)
            stream.Seek(-MaxLogBytes, SeekOrigin.End);

        using var reader = new StreamReader(stream);
        string text = reader.ReadToEnd();
        return cut ? "[earlier output is in the log file]\n" + text : text;
    }

    /// <summary>
    /// Runs a program, copying what it prints into the log, and fails the update if it fails.
    /// </summary>
    private async Task RunLoggedAsync(string fileName, IEnumerable<string> arguments)
    {
        int exitCode = await ProcessRunner.RunLoggedAsync(fileName, arguments, null, Log, CancellationToken.None);
        if (exitCode != 0)
            throw new UpdateException($"{fileName} failed with exit code {exitCode}.");
    }

    private Task<ProcessRunner.Result> GitAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken) =>
        ProcessRunner.RunAsync("git", ["-C", Settings.SourceDirectory!, .. arguments], null, timeout, cancellationToken);

    private async Task<string> RequireOutputAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var result = await GitAsync(arguments, QuickTimeout, cancellationToken);
        if (!result.Succeeded)
            throw new UpdateException($"git failed: {result.Error.Trim()}");

        return result.Output.Trim();
    }

    private static CommitInfo ParseCommit(string line)
    {
        string[] fields = line.Split('\u001f');
        return new CommitInfo(
            fields[0],
            fields[1],
            DateTimeOffset.Parse(fields[2], CultureInfo.InvariantCulture).UtcDateTime,
            fields[3]);
    }

    private static string? CommitFromAssembly()
    {
        string? version = typeof(UpdateService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        int plus = version?.IndexOf('+') ?? -1;
        return plus < 0 ? null : version![(plus + 1)..];
    }
}
