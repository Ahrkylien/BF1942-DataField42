using DataField42.Enums;
using DataField42.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace DataField42;

public class FileRuleViewModel : INotifyPropertyChanged
{
    public static IReadOnlyList<IgnoreSyncScenarios> AllScenarios { get; } =
        Enum.GetValues(typeof(IgnoreSyncScenarios)).Cast<IgnoreSyncScenarios>().ToList();

    public static IReadOnlyList<Bf1942FileTypes> AllFileTypes { get; } =
        Enum.GetValues(typeof(Bf1942FileTypes)).Cast<Bf1942FileTypes>()
            .Where(t => t != Bf1942FileTypes.None).ToList();

    public event PropertyChangedEventHandler? PropertyChanged;

    private IgnoreSyncScenarios _scenario = IgnoreSyncScenarios.Always;
    private Bf1942FileTypes _fileType = AllFileTypes[0];
    private string _mod = "*";
    private string _fileName = "*";

    public IgnoreSyncScenarios Scenario
    {
        get => _scenario;
        set { _scenario = value; Notify(nameof(Scenario)); }
    }

    public Bf1942FileTypes FileType
    {
        get => _fileType;
        set { _fileType = value; Notify(nameof(FileType)); }
    }

    public string Mod
    {
        get => _mod;
        set { _mod = value; Notify(nameof(Mod)); }
    }

    public string FileName
    {
        get => _fileName;
        set { _fileName = value; Notify(nameof(FileName)); }
    }

    public bool IsValid =>
        _fileType != Bf1942FileTypes.None
        && !string.IsNullOrWhiteSpace(_mod)
        && !string.IsNullOrWhiteSpace(_fileName);

    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static FileRuleViewModel FromFileRule(FileRule rule) => new FileRuleViewModel
    {
        _scenario = rule.IgnoreSyncScenario,
        _fileType = rule.FileType,
        _mod = rule.Mod,
        _fileName = rule.FileName
    };

    public FileRule ToFileRule() => new FileRule(_scenario.ToString(), _fileType.ToString(), _mod, _fileName);
}
