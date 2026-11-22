using System.Globalization;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;
using DataField42.Website.Configuration;

namespace DataField42.Website.Services.ServerQuery;

/// <summary>
/// Reads the live server list from the master server, which publishes the answers it already
/// collected rather than making every visitor's page query hundreds of servers itself. The result
/// is cached briefly, because every page that shows servers needs the whole list.
/// </summary>
public sealed class MasterServerClient(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<WebsiteOptions> options,
    ILogger<MasterServerClient> logger)
{
    private const string CacheKey = nameof(MasterServerClient) + ".ServerList";

    private readonly WebsiteOptions _options = options.Value;

    /// <summary>
    /// Every listed server, or an empty list when the master server cannot be reached -- the
    /// pages that show servers stay up either way.
    /// </summary>
    public async Task<IReadOnlyList<MasterServerEntry>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<MasterServerEntry>? cached) && cached is not null)
            return cached;

        var servers = await FetchAsync(cancellationToken);
        cache.Set(CacheKey, servers, _options.ServerListCacheDuration);
        return servers;
    }

    private async Task<IReadOnlyList<MasterServerEntry>> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await httpClient.GetStreamAsync(_options.MasterServerUrl, cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var servers = new List<MasterServerEntry>();
            foreach (var element in document.RootElement.EnumerateArray())
                if (TryReadEntry(element) is { } entry)
                    servers.Add(entry);

            return servers;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not read the server list from {Url}", _options.MasterServerUrl);
            return [];
        }
    }

    private MasterServerEntry? TryReadEntry(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("IP", out var ipElement)
            || ipElement.GetString() is not { Length: > 0 } ip
            || !element.TryGetProperty("query", out var queryElement)
            || queryElement.ValueKind != JsonValueKind.Object)
            return null;

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in queryElement.EnumerateObject())
        {
            if (property.NameEquals("players"))
            {
                FlattenPlayers(property.Value, properties);
                continue;
            }

            if (ToPropertyValue(property.Value) is { } value)
                properties[property.Name] = value;
        }

        Bf1942QueryResult status;
        try
        {
            status = new Bf1942QueryResult(properties);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ProtocolViolationException)
        {
            // A real server reports every property the protocol defines. Something that does not
            // is not a Battlefield 1942 server -- a browser reimplementation announcing itself to
            // the master server, say -- so it is left off the list rather than shown half-read.
            logger.LogDebug(ex, "Ignoring {Ip}, which does not report a full server status", ip);
            return null;
        }

        return new MasterServerEntry
        {
            Ip = ip,
            ReportedPlayerCount = GetInt(queryElement, "numplayers") is { } reported and >= 0 ? (uint)reported : null,
            QueryPort = GetInt(element, "queryPort"),
            QueriedAt = GetTimestamp(element, "query_timestamp"),
            HeartbeatAt = GetTimestamp(element, "heartbeat_timestamp"),
            Source = element.TryGetProperty("source", out var source) ? source.GetString() : null,
            Status = status,
        };
    }

    /// <summary>
    /// The master server publishes the scoreboard as a list of objects, while the protocol itself
    /// numbers the properties per player. Flattening the list back into that shape is what lets a
    /// listed server and a directly queried one be read by the same code.
    /// </summary>
    private static void FlattenPlayers(JsonElement playersElement, Dictionary<string, string> properties)
    {
        if (playersElement.ValueKind != JsonValueKind.Array)
            return;

        int index = 0;
        foreach (var playerElement in playersElement.EnumerateArray())
        {
            if (playerElement.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var property in playerElement.EnumerateObject())
                if (ToPropertyValue(property.Value) is { } value)
                    properties[$"{property.Name}_{index}"] = value;

            index++;
        }

        properties["numplayers"] = index.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The master server publishes already-typed JSON, so a value arrives as a number, a boolean
    /// or a list where the protocol only ever had strings. Everything is normalized back to the
    /// protocol's own spelling.
    /// </summary>
    private static string? ToPropertyValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.ToString(),
        JsonValueKind.True => "1",
        JsonValueKind.False => "0",
        JsonValueKind.Array => string.Join(',', value.EnumerateArray().Select(item => item.ToString())),
        _ => null,
    };

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out int number) => number,
            JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            _ => null,
        };
    }

    private static DateTimeOffset? GetTimestamp(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
            return null;

        return DateTimeOffset.FromUnixTimeMilliseconds((long)(property.GetDouble() * 1000));
    }
}
