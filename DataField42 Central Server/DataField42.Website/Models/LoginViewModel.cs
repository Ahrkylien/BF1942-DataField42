namespace DataField42.Website.Models;

/// <summary>
/// The sign-in form.
/// </summary>
public sealed class LoginViewModel
{
    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>
    /// Where to go once signed in, which is the admin page that redirected here.
    /// </summary>
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the accounts could not be read at all, in which case the form is pointless and is
    /// hidden.
    /// </summary>
    public bool DatabaseUnavailable { get; set; }

    /// <summary>
    /// Whether no account exists yet, in which case the page explains how to create the first one
    /// rather than leaving someone stuck at a form nothing will satisfy.
    /// </summary>
    public bool NeedsFirstAccount { get; set; }
}
