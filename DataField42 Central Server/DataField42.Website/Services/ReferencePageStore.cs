using Microsoft.Extensions.Caching.Memory;

namespace DataField42.Website.Services;

/// <summary>
/// The reference pages: the generated listings of every engine command, and the collected
/// errors from one mod's scripts. They are large, fixed HTML documents with no data of their
/// own, so they are kept as files and read at run time rather than compiled as views -- a
/// megabyte of markup turns into a megabyte of generated C# otherwise, which dominates the
/// build.
/// </summary>
public sealed class ReferencePageStore(IWebHostEnvironment environment, IMemoryCache cache, ILogger<ReferencePageStore> logger)
{
    private const string Directory = "Resources/reference";

    /// <summary>
    /// The contents of one reference page, or <see langword="null"/> when it is missing. Read
    /// once and then held in memory: the files never change while the site is running.
    /// </summary>
    public string? Read(string name)
    {
        return cache.GetOrCreate($"{nameof(ReferencePageStore)}:{name}", _ =>
        {
            string path = Path.Combine(environment.ContentRootPath, Directory, $"{name}.html");
            if (!File.Exists(path))
            {
                logger.LogError("Reference page {Path} is missing", path);
                return null;
            }

            return File.ReadAllText(path);
        });
    }
}
