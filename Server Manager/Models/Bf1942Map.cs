public class Bf1942Map
{
    public string Name { get; set; }

    public List<GamePlayMode> GamePlayModes { get; set; } = [];

    public string Mod { get; set; }

    public Bf1942Map(string mod, string name, string gamePlayModes)
    {
        if (!byte.TryParse(gamePlayModes, out byte gamePlayModesByte) || gamePlayModesByte > 31)
            throw new ArgumentException($"Game Play Mode bits are not in a valid format: {gamePlayModes}");

        if ((gamePlayModesByte & 0b00001) != 0)
            GamePlayModes.Add(GamePlayMode.GPM_CQ);
        if ((gamePlayModesByte & 0b00010) != 0)
            GamePlayModes.Add(GamePlayMode.GPM_COOP);
        if ((gamePlayModesByte & 0b00100) != 0)
            GamePlayModes.Add(GamePlayMode.GPM_CTF);
        if ((gamePlayModesByte & 0b01000) != 0)
            GamePlayModes.Add(GamePlayMode.GPM_TDM);
        if ((gamePlayModesByte & 0b10000) != 0)
            GamePlayModes.Add(GamePlayMode.GPM_OBJECTIVEMODE);
        Name = name;
        Mod = mod;
    }

    public override string ToString() => $"{Name} {Mod} {string.Join(" ", GamePlayModes)}";
}
