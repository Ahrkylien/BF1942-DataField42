public class FileRule
{
    public IgnoreSyncScenario IgnoreSyncScenario { get; }
    public Bf1942FileType FileType { get; }
    public string Mod { get; }
    public string FileName { get; }

    private bool AllMods => Mod == "*";
    private bool AllFiles => FileName == "*";

    public FileRule(string ignoreSyncScenario, string fileType, string mod, string fileName)
    {
        IgnoreSyncScenario = Enum.Parse<IgnoreSyncScenario>(ignoreSyncScenario, ignoreCase: true);
        FileType = Enum.Parse<Bf1942FileType>(fileType, ignoreCase: true);
        Mod = mod.ToLower();
        FileName = CorrectFileName(fileName, FileType);
    }

    public FileRule(IgnoreSyncScenario ignoreSyncScenarios, Bf1942FileType fileType, string mod, string fileName)
    {
        IgnoreSyncScenario = ignoreSyncScenarios;
        FileType = fileType;
        Mod = mod.ToLower();
        FileName = CorrectFileName(fileName, FileType);
    }

    private static string CorrectFileName(string fileName, Bf1942FileType fileType)
    {
        fileName = fileName.ToLower();

        // append file extension to FileName if not provided
        if (fileName != "*")
        {
            if ((fileType == Bf1942FileType.Level || fileType == Bf1942FileType.Archive) && !fileName.EndsWith(".rfa"))
                fileName += ".rfa";
            else if ((fileType == Bf1942FileType.Movie || fileType == Bf1942FileType.Music) && !fileName.EndsWith(".bik"))
                fileName += ".bik";
            else if (fileType == Bf1942FileType.ModMiscFile)
            {
                switch (fileName)
                {
                    case "contentcrc32":
                    case "init":
                        fileName += ".con";
                        break;
                    case "mod":
                        fileName += ".dll";
                        break;
                    case "lexiconall":
                        fileName += ".dat";
                        break;
                    case "serverinfo":
                        fileName += ".dds";
                        break;
                }
            }
        }
        return fileName;
    }

    public bool Matches(FileInfo fileInfo) => 
        (AllMods || Mod.ToLower() == fileInfo.Mod.ToLower())
        && FileType == fileInfo.FileType
        && (AllFiles || FileName.ToLower() == fileInfo.FileNameWithoutPatchNumber.ToLower());

    public string Serialize() => $"{IgnoreSyncScenario} {FileType} {Mod} {FileName}";
}
