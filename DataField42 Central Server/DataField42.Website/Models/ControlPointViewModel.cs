namespace DataField42.Website.Models;

/// <summary>
/// A control point placed on the minimap, positioned as a percentage of the image so the markers
/// scale with it.
/// </summary>
public sealed record ControlPointViewModel(int Id, string Name, string? Team, bool Uncapturable, double RelativeLeft, double RelativeTop);
