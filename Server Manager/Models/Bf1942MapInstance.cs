public class Bf1942MapInstance
{
    public string Name { get; init; }

    public GamePlayMode GamePlayMode { get; init; }

    public string Mod { get; init; }

    public Bf1942MapInstance(string name, GamePlayMode gamePlayMode, string mod)
    {
        Name = name;
        GamePlayMode = gamePlayMode;
        Mod = mod;
    }

    public Bf1942MapInstance(string nameGamePlayModeMod)
    {
        var parts = nameGamePlayModeMod.Split(' ');
        if (parts.Length != 3)
            throw new ArgumentException($"Map is not in a valid format: {nameGamePlayModeMod}");
        if (!Enum.TryParse(parts[1], out GamePlayMode gamePlayMode))
            throw new ArgumentException($"Game Play Mode is not in a valid format: {parts[1]} in ({nameGamePlayModeMod})");
        Name = parts[0];
        GamePlayMode = gamePlayMode;
        Mod = parts[2];
    }

    public override string ToString() => $"{Name} {GamePlayMode} {Mod}";
}
