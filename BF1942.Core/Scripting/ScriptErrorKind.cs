namespace BF1942.Core.Scripting;

/// <summary>
/// What went wrong with a script, so that a caller can leave out the kinds it does not care about.
/// </summary>
public enum ScriptErrorKind
{
    /// <summary>
    /// A script that another one runs is not in the file system.
    /// </summary>
    FileNotFound,

    /// <summary>
    /// Any other error, such as a line that could not be executed. Kinds that are told apart get
    /// their own value, which makes this one smaller.
    /// </summary>
    OtherError,
}
