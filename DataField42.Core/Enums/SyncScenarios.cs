public enum SyncScenarios
{
    Always,
    /// <summary>
    /// This scenario is when no singe version of that file is in the game dir
    /// </summary>
    NoFileYet,
    Never,
}

public enum IgnoreSyncScenarios
{
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