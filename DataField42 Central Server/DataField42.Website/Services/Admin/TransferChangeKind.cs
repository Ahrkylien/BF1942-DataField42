namespace DataField42.Website.Services.Admin;

/// <summary>
/// What happens to one file once the rules that concern it are taken together.
/// </summary>
public enum TransferChangeKind
{
    /// <summary>
    /// A file that is not here yet.
    /// </summary>
    Added,

    /// <summary>
    /// A file here is removed and a new version of it added at the same path.
    /// </summary>
    Replaced,

    Deleted,

    /// <summary>
    /// A file is renamed or moved in place. Its date may change as well.
    /// </summary>
    Moved,

    /// <summary>
    /// A file already here is copied to another path. Its date may change as well.
    /// </summary>
    Copied,

    /// <summary>
    /// Only the modification date of a file changes.
    /// </summary>
    Redated,

    /// <summary>
    /// A rule this server does not understand.
    /// </summary>
    NotUnderstood,
}
