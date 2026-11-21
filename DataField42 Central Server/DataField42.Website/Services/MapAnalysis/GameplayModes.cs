namespace DataField42.Website.Services.MapAnalysis;

/// <summary>
/// Which gameplay modes a level ships, determined by the presence of the per-mode script the
/// engine loads for it.
/// </summary>
public readonly record struct GameplayModes(bool Conquest, bool CaptureTheFlag, bool TeamDeathmatch, bool Coop, bool ObjectiveMode)
{
    public bool Any => Conquest || CaptureTheFlag || TeamDeathmatch || Coop || ObjectiveMode;
}
