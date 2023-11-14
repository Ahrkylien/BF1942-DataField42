public class FileRule
{
    public SyncScenarios SyncScenario { get; private set; }
    public Bf1942FileTypes FileType { get; private set; }
    public string Mod { get; private set; }
    public string FileName { get; private set; }
    public string FilePath { get; private set; }

    public bool AllMods => Mod == "*";
    public bool AllFiles => FileName == "*";

    public FileRule(string syncScenario, string fileType, string mod, string fileName)
    {
        SyncScenario = Enum.Parse<SyncScenarios>(syncScenario, true);
        FileType = Enum.Parse<Bf1942FileTypes>(fileType, true);
        Mod = mod.ToLower();
        FileName = fileName.ToLower();

        // append file type to FileName if not provided
        if ((FileType == Bf1942FileTypes.Level || FileType == Bf1942FileTypes.Archive) && !FileName.EndsWith(".rfa"))
            FileName += ".rfa";
        else if ((FileType == Bf1942FileTypes.Movie || FileType == Bf1942FileTypes.Music) && !FileName.EndsWith(".bik"))
            FileName += ".bik";
        else if (FileType == Bf1942FileTypes.ModMiscFile)
        {
            switch(FileName)
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
        switch (FileType)
        {
            case Bf1942FileTypes.Movie:
                FilePath = "movies/" + FileName;
                break;
            case Bf1942FileTypes.Music:
                FilePath = "music/" + FileName;
                break;
            case Bf1942FileTypes.ModMiscFile:
            case Bf1942FileTypes.Archive:
                FilePath = FileName;
                break;
            case Bf1942FileTypes.Level:
                FilePath = "archives/bf1942/levels/" + FileName;
                break;
        }
    }

    public bool VerifyIgnore(FileInfo fileInfo, bool existsWithDifferentVersion)
    {
        if (FileName == fileInfo.FilePath || AllFiles) // TODO: FileName is not FilePath
            if (Mod == fileInfo.Mod || AllMods)
                if (SyncScenario == SyncScenarios.Always || (SyncScenario == SyncScenarios.DifferentVersion && existsWithDifferentVersion))
                    return true;
        return false;

    }
}
