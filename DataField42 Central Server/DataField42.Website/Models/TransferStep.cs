namespace DataField42.Website.Models;

/// <summary>
/// One step of the file transfer pipeline, and where it stands.
/// </summary>
/// <param name="Title">What the step is.</param>
/// <param name="Mark">A symbol, or the step's number while it has not been done.</param>
/// <param name="Color">The colour of the state.</param>
/// <param name="Text">A few words on the state.</param>
public sealed record TransferStep(string Title, string Mark, string Color, string Text);
