using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using DataField42.Website.Data.Entities;

using DataField42.Website.Services.Admin;

namespace DataField42.Website.Security;

/// <summary>
/// Names shared between the sign-in code, the authorization policy and the views.
/// </summary>
public static class AdminAuthentication
{
    public const string PolicyName = "Admin";

    public const string SecurityStampClaim = "security-stamp";

    public const string LoginPath = "/Login";

    public const string LogoutPath = "/Logout";

    /// <summary>
    /// The identity stored in the sign-in cookie.
    /// </summary>
    public static ClaimsPrincipal CreatePrincipal(AdminUser user) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(SecurityStampClaim, user.SecurityStamp.ToString()),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme));
}
