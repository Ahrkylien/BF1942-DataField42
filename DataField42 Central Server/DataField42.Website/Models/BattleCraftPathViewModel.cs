namespace DataField42.Website.Models;

/// <summary>
/// The BattleCraft project path recovered from a map's archives, split into the mod and map folder
/// its author kept it under.
/// </summary>
public sealed record BattleCraftPathViewModel(string FullPath, string? ModFolder, string? MapFolder);
