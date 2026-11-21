namespace DataField42.Website.Services.MapAnalysis;

/// <summary>
/// One object placed in the level.
/// </summary>
public sealed class LevelObjectInfo(string templateName)
{
    public string TemplateName { get; } = templateName;

    /// <summary>
    /// The template this object instantiates, or <see langword="null"/> when the level references
    /// a template no loaded mod defines.
    /// </summary>
    public ObjectTemplateInfo? Template { get; internal set; }

    public string Name { get; internal set; } = "";

    public Vec3 AbsolutePosition { get; internal set; }

    public Vec3 Rotation { get; internal set; }

    public Vec3 GeometryScale { get; internal set; } = Vec3.One;

    /// <summary>
    /// The spawner's group id, which ties vehicle spawns to the control point that owns them.
    /// </summary>
    public string? OSId { get; internal set; }

    public string? Team { get; internal set; }
}
