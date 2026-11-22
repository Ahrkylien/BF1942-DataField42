namespace DataField42.Website.Services.Admin;

/// <summary>
/// How serious a finding about an uploaded rule set is.
/// </summary>
public enum TransferIssueSeverity
{
    /// <summary>
    /// Worth a look, but the transfer can still be applied.
    /// </summary>
    Warning,

    /// <summary>
    /// The transfer cannot be applied: a rule could not be carried out, or would do harm.
    /// </summary>
    Error,
}
