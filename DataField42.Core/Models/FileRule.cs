public class FileRule
{
    public IgnoreSyncScenarios IgnoreSyncScenario { get; }
    public Bf1942FileTypes FileType { get; }
    public string Mod { get; }
    public string FileName { get; }

    private bool AllMods => Mod == "*";
    private bool AllFiles => FileName == "*";

    public FileRule(string ignoreSyncScenario, string fileType, string mod, string fileName)
    {
        IgnoreSyncScenario = Enum.Parse<IgnoreSyncScenarios>(ignoreSyncScenario, ignoreCase: true);
        FileType = Enum.Parse<Bf1942FileTypes>(fileType, ignoreCase: true);
        Mod = mod.ToLower();
        FileName = fileName.ToLower();

        // append file extension to FileName if not provided
        if (FileName != "*")
        {
            if ((FileType == Bf1942FileTypes.Level || FileType == Bf1942FileTypes.Archive) && !FileName.EndsWith(".rfa"))
                FileName += ".rfa";
            else if ((FileType == Bf1942FileTypes.Movie || FileType == Bf1942FileTypes.Music) && !FileName.EndsWith(".bik"))
                FileName += ".bik";
            else if (FileType == Bf1942FileTypes.ModMiscFile)
            {
                switch (FileName)
                {
                    case "contentcrc32":
                    case "init":
                        FileName += ".con";
                        break;
                    case "mod":
                        FileName += ".dll";
                        break;
                    case "lexiconall":
                        FileName += ".dat";
                        break;
                    case "serverinfo":
                        FileName += ".dds";
                        break;
                }
            }
        }
    }

    public bool Matches(FileInfo fileInfo) => 
        (AllMods || Mod.ToLower() == fileInfo.Mod.ToLower())
        && FileType == fileInfo.FileType
        && (AllFiles || FileName.ToLower() == fileInfo.FileNameWithoutPatchNumber.ToLower());

    public string Serialize() => $"{IgnoreSyncScenario} {FileType} {Mod} {FileName}";
}
