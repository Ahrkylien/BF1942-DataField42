public class FileRule
{
    public IgnoreSyncScenarios IgnoreSyncScenario { get; init; }
    private readonly Bf1942FileTypes _fileType;
    private readonly string _mod;
    private readonly string _fileName;

    private bool AllMods => _mod == "*";
    private bool AllFiles => _fileName == "*";

    public FileRule(string ignoreSyncScenario, string fileType, string mod, string fileName)
    {
        IgnoreSyncScenario = Enum.Parse<IgnoreSyncScenarios>(ignoreSyncScenario, true);
        _fileType = Enum.Parse<Bf1942FileTypes>(fileType, true);
        _mod = mod.ToLower();
        _fileName = fileName.ToLower();

        // append file extension to FileName if not provided
        if ((_fileType == Bf1942FileTypes.Level || _fileType == Bf1942FileTypes.Archive) && !_fileName.EndsWith(".rfa"))
            _fileName += ".rfa";
        else if ((_fileType == Bf1942FileTypes.Movie || _fileType == Bf1942FileTypes.Music) && !_fileName.EndsWith(".bik"))
            _fileName += ".bik";
        else if (_fileType == Bf1942FileTypes.ModMiscFile)
        {
            switch(_fileName)
            {
                case "contentcrc32":
                case "init":
                    _fileName += ".con";
                    break;
                case "mod":
                    _fileName += ".dll";
                    break;
                case "lexiconall":
                    _fileName += ".dat";
                    break;
                case "serverinfo":
                    _fileName += ".dds";
                    break;
            }
        }
    }

    public bool Matches(FileInfo fileInfo) => 
        (AllMods || _mod.ToLower() == fileInfo.Mod.ToLower())
        && _fileType == fileInfo.FileType
        && (AllFiles || _fileName.ToLower() == fileInfo.FilePath.ToLower());
}
