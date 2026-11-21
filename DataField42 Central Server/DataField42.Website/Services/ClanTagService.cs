using System.Text.Json;
namespace DataField42.Website.Services;

/// <summary>
/// Splits the clan tag off a player name, so scoreboards can show the tag separately and name
/// the clan it belongs to. Tags are read from <c>Resources/clanTags.json</c>, longest first, so
/// <c>-=Joker$=-</c> wins over a shorter tag that is a prefix of it.
/// </summary>
public sealed class ClanTagService
{
    private readonly List<KeyValuePair<string, string>> _tags;

    public ClanTagService(IWebHostEnvironment environment, ILogger<ClanTagService> logger)
    {
        string path = Path.Combine(environment.ContentRootPath, "Resources", "clanTags.json");
        try
        {
            var tags = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
            _tags = [.. tags.OrderByDescending(t => t.Key.Length)];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogError(ex, "Could not read clan tags from {Path}; player names will not be split", path);
            _tags = [];
        }
    }

    /// <summary>
    /// The clan tag <paramref name="playerName"/> starts with, or <see langword="null"/> when it
    /// starts with none.
    /// </summary>
    public ClanTagMatch? Match(string? playerName)
    {
        if (string.IsNullOrEmpty(playerName))
            return null;

        foreach (var (tag, clanName) in _tags)
            if (playerName.StartsWith(tag, StringComparison.Ordinal))
                return new ClanTagMatch(tag, clanName, playerName[tag.Length..]);

        return null;
    }
}
