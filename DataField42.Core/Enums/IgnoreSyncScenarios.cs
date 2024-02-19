public enum IgnoreSyncScenarios
{
    /// <summary>
    /// In this scenario the file(s) will not be synced (ignored during sync).
    /// </summary>
    Always,
    /// <summary>
    /// This scenario is when there is already (another) version of that file in the game dir
    /// </summary>
    DifferentVersion,
    /// <summary>
    /// this is used to exclude certain files from a group of ignore files
    /// </summary>
    Never,
    // WhenDependency, PartOfParentMod, NotPartOfCurrentMod, PartOfASourceMod, PartOfAParenteMod
}