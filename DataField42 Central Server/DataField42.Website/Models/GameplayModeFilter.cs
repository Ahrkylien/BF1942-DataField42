namespace DataField42.Website.Models;

/// <summary>
/// The gameplay-mode filter buttons, in the order the maps page shows them.
/// </summary>
public sealed class GameplayModeFilter
{
    public bool Coop { get; set; }

    public bool Conquest { get; set; }

    public bool CaptureTheFlag { get; set; }

    public bool TeamDeathmatch { get; set; }

    public bool ObjectiveMode { get; set; }

    /// <summary>
    /// Whether any mode is selected. With none selected the filter does nothing, so every mode
    /// counts as selected instead.
    /// </summary>
    public bool Any => Coop || Conquest || CaptureTheFlag || TeamDeathmatch || ObjectiveMode;

    /// <summary>
    /// The button label and state pairs, labelled as they appear in the <c>gpm</c> query string.
    /// </summary>
    public IEnumerable<(string Label, bool Selected)> Buttons =>
    [
        ("Coop", Coop),
        ("CQ", Conquest),
        ("CTF", CaptureTheFlag),
        ("TDM", TeamDeathmatch),
        ("Objective", ObjectiveMode),
    ];

    public static GameplayModeFilter All => new()
    {
        Coop = true,
        Conquest = true,
        CaptureTheFlag = true,
        TeamDeathmatch = true,
        ObjectiveMode = true,
    };

    /// <summary>
    /// Parses the <c>gpm</c> query string, which is a <c>+</c>-separated list of the button
    /// labels. An empty or unrecognised value selects every mode.
    /// </summary>
    public static GameplayModeFilter Parse(string? value)
    {
        var selected = (value ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries);
        var filter = new GameplayModeFilter
        {
            Coop = selected.Contains("Coop", StringComparer.OrdinalIgnoreCase),
            Conquest = selected.Contains("CQ", StringComparer.OrdinalIgnoreCase),
            CaptureTheFlag = selected.Contains("CTF", StringComparer.OrdinalIgnoreCase),
            TeamDeathmatch = selected.Contains("TDM", StringComparer.OrdinalIgnoreCase),
            ObjectiveMode = selected.Contains("Objective", StringComparer.OrdinalIgnoreCase),
        };

        return filter.Any ? filter : All;
    }
}
