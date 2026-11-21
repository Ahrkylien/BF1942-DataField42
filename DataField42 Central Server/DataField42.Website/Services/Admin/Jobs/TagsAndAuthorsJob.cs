using DataField42.Website.Data;
using DataField42.Website.Data.Entities;
using Microsoft.EntityFrameworkCore;
using YamlDotNet.Serialization;

namespace DataField42.Website.Services.Admin.Jobs;

/// <summary>
/// Re-imports who made which map and which maps belong to which league or event, from
/// <c>Resources/authors.yaml</c> and <c>Resources/tags.yaml</c>. Neither can be worked out from
/// the files, so both are maintained by hand and this job is what applies them.
/// </summary>
public sealed class TagsAndAuthorsJob(DataField42DbContext context, IWebHostEnvironment environment) : IAdminJob
{
    /// <summary>
    /// The colour a tag gets when it is new. Colours are adjusted by hand afterwards; the import
    /// never overwrites one.
    /// </summary>
    private const string DefaultTagColor = "#3f5172";

    private const string DefaultTagHoverColor = "#4d6490";

    public string JobId => "TagsAndAuthors";

    public string Name => "Import authors and tags";

    public string Description =>
        "Re-reads authors.yaml and tags.yaml and relinks every map to its author and tags. "
        + "Maps named in those files that are not in the database are reported.";

    public async Task RunAsync(JobContext jobContext, CancellationToken cancellationToken)
    {
        var authors = ReadMapping("authors.yaml");
        var tags = ReadMapping("tags.yaml");

        jobContext.BeginStep("Importing authors", authors.Count);
        await ImportAuthorsAsync(authors, jobContext, cancellationToken);

        jobContext.BeginStep("Importing tags", tags.Count);
        await ImportTagsAsync(tags, jobContext, cancellationToken);

        jobContext.Log("Done.");
    }

    private async Task ImportAuthorsAsync(MapMapping authors, JobContext jobContext, CancellationToken cancellationToken)
    {
        var existing = await context.Authors.ToListAsync(cancellationToken);

        // An author no longer named in the file is removed, which is what makes a rename in the
        // file take effect rather than leaving both spellings behind.
        foreach (var author in existing.Where(a => a.Name is not null && !authors.ContainsKey(a.Name)))
        {
            context.Authors.Remove(author);
            jobContext.Log($"Removed author {author.Name}");
        }

        foreach (var name in authors.Keys)
        {
            if (existing.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            context.Authors.Add(new Author { Name = name });
            jobContext.Log($"Added author {name}");
        }

        await context.SaveChangesAsync(cancellationToken);

        // Every map's author is cleared first, so a map dropped from the file loses its author.
        await context.Maps.ExecuteUpdateAsync(map => map.SetProperty(m => m.AuthorId, (int?)null), cancellationToken);

        var authorsByName = await context.Authors
            .Where(author => author.Name != null)
            .ToDictionaryAsync(author => author.Name!, author => author.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var (authorName, mapsByMod) in authors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!authorsByName.TryGetValue(authorName, out int authorId))
                continue;

            foreach (var mapId in await ResolveMapIdsAsync(mapsByMod, authorName, jobContext, cancellationToken))
                await context.Maps
                    .Where(map => map.Id == mapId)
                    .ExecuteUpdateAsync(map => map.SetProperty(m => m.AuthorId, authorId), cancellationToken);

            jobContext.Advance();
        }
    }

    private async Task ImportTagsAsync(MapMapping tags, JobContext jobContext, CancellationToken cancellationToken)
    {
        var existing = await context.Tags.ToListAsync(cancellationToken);

        foreach (var name in tags.Keys)
        {
            if (existing.Any(tag => string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            context.Tags.Add(new Tag { Name = name, Color = DefaultTagColor, ColorHover = DefaultTagHoverColor });
            jobContext.Log($"Added tag {name}");
        }

        await context.SaveChangesAsync(cancellationToken);

        var tagsByName = await context.Tags
            .Where(tag => tag.Name != null)
            .ToDictionaryAsync(tag => tag.Name!, tag => tag.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Links for tags the file still defines are rebuilt wholesale; links for tags it no
        // longer mentions are left alone, since those may have been made by hand.
        var importedTagIds = tags.Keys
            .Where(tagsByName.ContainsKey)
            .Select(name => tagsByName[name])
            .ToList();

        await context.TagLinks
            .Where(link => link.TagId != null && importedTagIds.Contains(link.TagId.Value))
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var (tagName, mapsByMod) in tags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!tagsByName.TryGetValue(tagName, out int tagId))
                continue;

            foreach (var mapId in await ResolveMapIdsAsync(mapsByMod, tagName, jobContext, cancellationToken))
                context.TagLinks.Add(new TagLink { MapId = mapId, TagId = tagId });

            jobContext.Advance();
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves the maps one entry names. Map names are written with spaces or underscores
    /// interchangeably in these files, so both spellings are matched.
    /// </summary>
    private async Task<List<int>> ResolveMapIdsAsync(
        Dictionary<string, List<string>> mapsByMod,
        string entryName,
        JobContext jobContext,
        CancellationToken cancellationToken)
    {
        var mapIds = new List<int>();

        foreach (var (modFolderName, mapNames) in mapsByMod)
        {
            var mod = await context.Mods
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.FolderName == modFolderName, cancellationToken);

            if (mod is null)
            {
                jobContext.Log($"{entryName}: no mod called {modFolderName}");
                continue;
            }

            foreach (var mapName in mapNames)
            {
                string nameRaw = mapName.Replace(' ', '_');
                var map = await context.Maps
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ModId == mod.Id && m.NameRaw == nameRaw, cancellationToken);

                if (map is null)
                {
                    jobContext.Log($"{entryName}: no map called {mapName} in {modFolderName}");
                    continue;
                }

                mapIds.Add(map.Id);
            }
        }

        return mapIds;
    }

    /// <summary>
    /// Reads one of the hand-maintained files, whose shape is
    /// <c>entry name -&gt; mod folder -&gt; map names</c>.
    /// </summary>
    private MapMapping ReadMapping(string fileName)
    {
        string path = Path.Combine(environment.ContentRootPath, "Resources", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"'{fileName}' is missing from the Resources folder.", path);

        var parsed = new DeserializerBuilder().Build().Deserialize<MapMapping>(File.ReadAllText(path));
        return parsed ?? [];
    }

    /// <summary>
    /// An author or tag, the mods it covers, and the maps within each.
    /// </summary>
    private sealed class MapMapping : Dictionary<string, Dictionary<string, List<string>>>
    {
        public MapMapping()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
