namespace DataField42.Website.Services.MapAnalysis;

/// <summary>
/// An object template, carrying only the properties the website reports on: control-point
/// identity and what an object spawner spawns per team.
/// </summary>
public sealed class ObjectTemplateInfo(string type, string name)
{
    public string Type { get; } = type;

    public string Name { get; } = name;

    public string ControlPointName { get; internal set; } = "";

    public string? Team { get; internal set; }

    public string? UnableToChangeTeam { get; internal set; }

    /// <summary>
    /// What this spawner spawns, keyed by the team slot of
    /// <c>objectTemplate.setObjectTemplate &lt;team&gt; &lt;name&gt;</c>.
    /// </summary>
    public Dictionary<int, string> SpawnedTemplateNames { get; } = [];

    public bool IsControlPoint => Type.Equals("controlpoint", StringComparison.OrdinalIgnoreCase);

    public bool IsObjectSpawner => Type.Equals("objectspawner", StringComparison.OrdinalIgnoreCase);
}
