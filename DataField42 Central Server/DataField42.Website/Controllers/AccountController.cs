using DataField42.Website.Data;
using DataField42.Website.Models;
using DataField42.Website.Security;
using DataField42.Website.Services.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace DataField42.Website.Controllers;

/// <summary>
/// Signing in and out. The sign-in page is deliberately not linked from anywhere: the admin pages
/// redirect to it when they are reached without a session, and that is the only way in.
/// </summary>
public sealed class AccountController(AdminUserService users, ILogger<AccountController> logger) : Controller
{
    [HttpGet(AdminAuthentication.LoginPath)]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Sign in";

        var model = new LoginViewModel { ReturnUrl = returnUrl };

        // The accounts live in the database, so if it cannot be reached there is nothing to sign
        // in against. The page says so rather than failing.
        try
        {
            model.NeedsFirstAccount = !await users.HasAnyUserAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseFailure.IsUnavailable(ex))
        {
            logger.LogError(ex, "Could not read the admin accounts");
            model.ErrorMessage = "The database cannot be reached, so signing in is not possible right now.";
            model.DatabaseUnavailable = true;
        }

        return View(model);
    }

    [HttpPost(AdminAuthentication.LoginPath)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Sign in";

        try
        {
            model.NeedsFirstAccount = !await users.HasAnyUserAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseFailure.IsUnavailable(ex))
        {
            logger.LogError(ex, "Could not read the admin accounts");
            model.ErrorMessage = "The database cannot be reached, so signing in is not possible right now.";
            model.DatabaseUnavailable = true;
            model.Password = null;
            return View(model);
        }

        if (string.IsNullOrEmpty(model.UserName) || string.IsNullOrEmpty(model.Password))
        {
            model.ErrorMessage = "Enter a username and password.";
            return View(model);
        }

        var (result, user) = await users.ValidateAsync(model.UserName, model.Password, cancellationToken);
        if (result != AdminSignInResult.Succeeded || user is null)
        {
            logger.LogWarning("Failed sign-in attempt for {UserName}", model.UserName);
            model.ErrorMessage = "That username and password do not match.";
            model.Password = null;
            return View(model);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AdminAuthentication.CreatePrincipal(user),
            new AuthenticationProperties { IsPersistent = true });

        return Redirect(GetSafeReturnUrl(model.ReturnUrl));
    }

    [HttpPost(AdminAuthentication.LogoutPath)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    /// <summary>
    /// Only a path within this site is followed after signing in, so a crafted link cannot turn
    /// the sign-in page into a redirect to somewhere else.
    /// </summary>
    private string GetSafeReturnUrl(string? returnUrl) =>
        returnUrl is { Length: > 0 } && Url.IsLocalUrl(returnUrl) ? returnUrl : "/Admin";
}
