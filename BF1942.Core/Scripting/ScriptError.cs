namespace BF1942.Core.Scripting;

/// <summary>
/// One line that failed to execute -- collected, not thrown, so one bad line doesn't abort
/// loading the other several thousand across a full mod (matches <c>BF42_script.read</c>'s own
/// per-line try/except that only logs to stderr and continues).
/// </summary>
public sealed record ScriptError(string Path, int LineNumber, string Line, string Message);
