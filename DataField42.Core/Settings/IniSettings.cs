using System.Net;

namespace DataField42.Settings;

public class IniSettings
{
    public ApplicationSection Application { get; set; } = new();

    public SynchronisationRulesSection SynchronisationRules { get; set; } = new();
}