namespace DataField42.Website.Services.Admin;

/// <summary>
/// One line of the review: what happens to one file, made from the rules that concern it, with what
/// is wrong with it. An add and a delete at the same path are one replaced file, a move and a
/// re-dating of its destination are one moved file.
/// </summary>
/// <param name="Kind">What happens to the file.</param>
/// <param name="Path">The file's path after the transfer; for a delete, the path it had.</param>
/// <param name="SourcePath">Where a moved or copied file comes from.</param>
/// <param name="NewDate">The modification date the file ends up with, when a rule sets one.</param>
/// <param name="OldDate">The modification date the file has now, when it is there to ask.</param>
/// <param name="Issues">What is wrong with it, errors first.</param>
public sealed record TransferChange(
    TransferChangeKind Kind,
    string Path,
    string? SourcePath,
    DateTime? NewDate,
    DateTime? OldDate,
    IReadOnlyList<TransferIssue> Issues);
