using System.Security.Claims;
using DataField42.Website.Services.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
namespace DataField42.Website.Security;

/// <summary>
/// Rejects a sign-in cookie whose account has since been deleted or had its password changed, so
/// a password change really does end the old sessions.
/// </summary>
public sealed class AdminCookieEvents : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userName = context.Principal?.FindFirstValue(ClaimTypes.Name);
        var stampClaim = context.Principal?.FindFirstValue(AdminAuthentication.SecurityStampClaim);
        if (userName is null || stampClaim is null)
        {
            await RejectAsync(context);
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<AdminUserService>();
        var user = await users.FindAsync(userName, context.HttpContext.RequestAborted);
        if (user is null || user.SecurityStamp.ToString() != stampClaim)
            await RejectAsync(context);
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
