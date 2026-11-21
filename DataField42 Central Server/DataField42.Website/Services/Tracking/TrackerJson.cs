using System.Globalization;
using System.Text.Json;

namespace DataField42.Website.Services.Tracking;

/// <summary>
/// Reads values out of the master server's JSON, which is typed but not consistently: a number can
/// arrive as a string, and a property can be missing.
/// </summary>
internal static class TrackerJson
{
    public static int? Int(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out int number) => number,
            JsonValueKind.Number => (int)property.GetDouble(),
            JsonValueKind.String when int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            _ => null,
        };
    }

    public static string? String(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    public static double? Double(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number ? property.GetDouble() : null;

    public static JsonElement? Object(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Object ? property : null;

    /// <summary>
    /// A time the master server publishes as seconds since 1970, as local time cut to the second:
    /// the form rounds are stored and looked up in.
    /// </summary>
    public static DateTime LocalTime(double unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds((long)Math.Floor(unixSeconds)).LocalDateTime;
}
