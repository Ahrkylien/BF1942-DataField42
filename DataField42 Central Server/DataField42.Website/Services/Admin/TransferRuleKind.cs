namespace DataField42.Website.Services.Admin;

/// <summary>
/// What one rule in a transfer does.
/// </summary>
public enum TransferRuleKind
{
    Delete,
    Move,
    Copy,
    ChangeModifiedDate,
    Add,
    Unknown,
}
