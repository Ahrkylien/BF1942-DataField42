using System;
using System.Text;
using YamlDotNet.Core.Tokens;

namespace DataField42.Settings;

class IniFile
{
    private readonly string _filePath;

    private readonly Dictionary<string, Dictionary<string, string>> _settings = new();

    public IniFile(string filePath)
    {
        _filePath = filePath;
    }

    public void AddSections(params string[] sections)
    {
        foreach (var section in sections)
            if (!_settings.ContainsKey(section))
                _settings[section] = new();
    }

    public void Add(string section, string key, string value)
    {
        AddSections(section);
        _settings[section][key] = value;
    }

    public void Add(string section, string key, IEnumerable<string> values)
    {
        var i = 0;
        foreach (var value in values)
            Add(section, $"{key}:{i++}", value);
    }

    public void Add(string section, string key, string[] values) => Add(section, key, (IEnumerable<string>)values);

    public void Save()
    {
        var sb = new StringBuilder();
        foreach ((var sectionName, var sectionSettings) in _settings)
        {
            sb.AppendLine($"[{sectionName}]");
            foreach ((var settingName, var settingValue) in sectionSettings)
            {
                sb.AppendLine($"{settingName} = {settingValue}");
            }
            sb.AppendLine("");
        }

        FileHelper.WriteText(_filePath, sb.ToString());
    }
}