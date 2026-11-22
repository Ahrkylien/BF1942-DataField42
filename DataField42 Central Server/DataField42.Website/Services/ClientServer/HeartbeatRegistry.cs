using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DataField42.Website.Configuration;
using Microsoft.Extensions.Options;

namespace DataField42.Website.Services.ClientServer;

/// <summary>
/// The DataField42 servers that have said they are there. A server sends a heartbeat every minute,
/// and is listed for three minutes after the last.
/// </summary>
/// <remarks>
/// The list is kept in <c>heartbeat_records.json</c> next to the file transfer's working files, in the
/// format the earlier central server wrote, since something else may read it.
/// </remarks>
public sealed partial class HeartbeatRegistry(IOptions<WebsiteOptions> options, ILogger<HeartbeatRegistry> logger)
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(3);

    private readonly Lock _lock = new();
    private List<Record>? _records;

    private string RecordsPath => Path.Combine(options.Value.FileTransferDirectory, "heartbeat_records.json");

    /// <summary>
    /// Notes that a server is there. What it calls its version is kept only if it looks like one, since
    /// anyone can send a heartbeat and the list is read by other programs.
    /// </summary>
    public void Store(string ip, string version)
    {
        if (!VersionPattern().IsMatch(version))
        {
            logger.LogInformation("{Ip} sent a heartbeat whose version is not a version", ip);
            return;
        }

        using (_lock.EnterScope())
        {
            var now = DateTime.Now;
            var records = Records();

            // One record per server, the latest, and none that has gone quiet.
            records.RemoveAll(record => record.Ip == ip || now - ParseTime(record) > Lifetime);
            records.Add(new Record(ip, version, now.ToString(TimeFormat, CultureInfo.InvariantCulture)));

            Save(records);
        }
    }

    private List<Record> Records()
    {
        if (_records is not null)
            return _records;

        try
        {
            _records = File.Exists(RecordsPath) ? JsonSerializer.Deserialize<List<Record>>(File.ReadAllText(RecordsPath)) ?? [] : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Could not read {Path}; starting with no heartbeats", RecordsPath);
            _records = [];
        }

        return _records;
    }

    private void Save(List<Record> records)
    {
        try
        {
            Directory.CreateDirectory(options.Value.FileTransferDirectory);
            File.WriteAllText(RecordsPath, JsonSerializer.Serialize(records));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write {Path}", RecordsPath);
        }
    }

    [GeneratedRegex(@"^[0-9]{1,5}(\.[0-9]{1,5}){0,3}\z")]
    private static partial Regex VersionPattern();

    private static DateTime ParseTime(Record record) =>
        DateTime.TryParseExact(record.DateTime, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : DateTime.MinValue;

    private sealed record Record(
        [property: JsonPropertyName("ip")] string Ip,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("datetime")] string DateTime);
}
