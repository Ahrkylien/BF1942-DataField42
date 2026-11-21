namespace DataField42.Website.Services.Admin;

/// <summary>
/// Why a sign-in attempt did not succeed. Callers report all failures identically, so an attacker
/// cannot tell an unknown user from a wrong password.
/// </summary>
public enum AdminSignInResult
{
    Succeeded,
    Failed,
}
