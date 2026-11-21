using System.Globalization;
using System.Text.Json;
using DataField42.Website.Services;

namespace DataField42.Website.Models;

/// <summary>
/// The player-count graph. Points and annotations are serialized to the shapes the page's
/// Chart.js configuration expects: a point is <c>[timestamp, players]</c> and a timestamp is
/// written in UTC, which the page converts to the viewer's own zone.
/// </summary>
public sealed class LineGraphViewModel
{
    private const string TimestampFormat = "yyyy/MM/dd HH:mm:ss +0000";

    public required IReadOnlyList<PlayerCountSample> Samples { get; init; }

    public IReadOnlyList<GraphAnnotation> Annotations { get; init; } = [];

    /// <summary>
    /// The points as the JSON array the page assigns straight into its chart configuration.
    /// </summary>
    public string SamplesJson => JsonSerializer.Serialize(
        Samples.Select(sample => new object[] { sample.Date.ToString(TimestampFormat, CultureInfo.InvariantCulture), sample.Players }));

    /// <summary>
    /// The map-change markers, which the page turns into Chart.js annotations.
    /// </summary>
    public string AnnotationsJson => JsonSerializer.Serialize(
        Annotations.Select(annotation => new { label = annotation.Label, value = annotation.Value }));

    public static string FormatTimestamp(DateTime value) =>
        value.ToString(TimestampFormat, CultureInfo.InvariantCulture);
}
