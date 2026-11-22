namespace DataField42.Website.Services.Admin;

/// <summary>
/// One finding about an uploaded rule set or its archive.
/// </summary>
/// <param name="Severity">Whether the finding stops the transfer being applied.</param>
/// <param name="RuleIndex">The position of the rule it concerns, counted from 0, or <see langword="null"/> for a finding about the archive or the uploads as a whole.</param>
/// <param name="Path">The file it concerns, or empty for a finding about the uploads as a whole.</param>
/// <param name="Message">What is wrong, in a sentence.</param>
public sealed record TransferIssue(TransferIssueSeverity Severity, int? RuleIndex, string Path, string Message);
