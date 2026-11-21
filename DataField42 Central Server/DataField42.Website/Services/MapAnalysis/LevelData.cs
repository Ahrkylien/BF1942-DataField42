namespace DataField42.Website.Services.MapAnalysis;

/// <summary>
/// Everything one level's scripts declared, after templates and placed objects have been linked.
/// </summary>
public sealed class LevelData
{
    private readonly List<ObjectTemplateInfo> _objectTemplates = [];
    private readonly Dictionary<string, ObjectTemplateInfo> _objectTemplatesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LevelObjectInfo> _objects = [];
    private readonly Dictionary<string, LevelObjectInfo> _objectsByName = new(StringComparer.OrdinalIgnoreCase);

    public GameInfo Game { get; } = new();

    public int? ConsoleWorldSize { get; internal set; }

    public IReadOnlyList<ObjectTemplateInfo> ObjectTemplates => _objectTemplates;

    public IReadOnlyList<LevelObjectInfo> Objects => _objects;

    internal ObjectTemplateInfo? ActiveObjectTemplate { get; set; }

    internal LevelObjectInfo? ActiveObject { get; set; }

    public ObjectTemplateInfo? GetObjectTemplate(string name) => _objectTemplatesByName.GetValueOrDefault(name);

    public LevelObjectInfo? GetObject(string name) => _objectsByName.GetValueOrDefault(name);

    /// <summary>
    /// Creates a template, or returns <see langword="null"/> when one of that name already exists
    /// -- the first definition wins, which is how a mod's own templates take precedence over those
    /// of the mods it depends on.
    /// </summary>
    internal ObjectTemplateInfo? CreateObjectTemplate(string type, string name)
    {
        if (_objectTemplatesByName.ContainsKey(name))
            return null;

        var template = new ObjectTemplateInfo(type, name);
        _objectTemplates.Add(template);
        _objectTemplatesByName[name] = template;
        return template;
    }

    internal LevelObjectInfo CreateObject(string templateName)
    {
        var levelObject = new LevelObjectInfo(templateName);
        _objects.Add(levelObject);
        return levelObject;
    }

    internal void RegisterObjectName(LevelObjectInfo levelObject, string name)
    {
        levelObject.Name = name;
        if (name.Length > 0)
            _objectsByName[name] = levelObject;
    }

    /// <summary>
    /// Resolves each placed object's template name to the template itself. Deferred until every
    /// script has been read, because a level may place an object before the mod that defines its
    /// template has been loaded.
    /// </summary>
    public void Link()
    {
        foreach (var levelObject in _objects)
            levelObject.Template ??= GetObjectTemplate(levelObject.TemplateName);
    }
}
