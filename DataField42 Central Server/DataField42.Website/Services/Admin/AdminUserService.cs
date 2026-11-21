using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using DataField42.Website.Data;
using DataField42.Website.Data.Entities;

namespace DataField42.Website.Services.Admin;

/// <summary>
/// The admin accounts. Passwords are only ever stored as a hash from ASP.NET Core's own password
/// hasher, which salts each one and picks the iteration count.
/// </summary>
public sealed class AdminUserService(DataField42DbContext context, IPasswordHasher<AdminUser> passwordHasher, ILogger<AdminUserService> logger)
{
    public Task<bool> HasAnyUserAsync(CancellationToken cancellationToken = default) =>
        context.AdminUsers.AnyAsync(cancellationToken);

    public Task<AdminUser?> FindAsync(string userName, CancellationToken cancellationToken = default) =>
        context.AdminUsers.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);

    /// <summary>
    /// Checks a password and, on success, returns the account to sign in. A hash stored in an
    /// older format is rehashed in place.
    /// </summary>
    public async Task<(AdminSignInResult Result, AdminUser? User)> ValidateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userName, cancellationToken);
        if (user is null)
        {
            // Hash anyway, so a request for an unknown user takes as long as one for a known user.
            passwordHasher.HashPassword(new AdminUser(), password);
            return (AdminSignInResult.Failed, null);
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
            return (AdminSignInResult.Failed, null);

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwordHasher.HashPassword(user, password);

        user.LastLoginUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return (AdminSignInResult.Succeeded, user);
    }

    /// <summary>
    /// Creates an account. Returns <see langword="null"/> when the name is taken.
    /// </summary>
    public async Task<AdminUser?> CreateAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(userName, cancellationToken) is not null)
            return null;

        var user = new AdminUser
        {
            UserName = userName,
            CreatedUtc = DateTime.UtcNow,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        context.AdminUsers.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created admin account {UserName}", userName);
        return user;
    }

    /// <summary>
    /// Replaces a password and invalidates sessions issued against the old one.
    /// </summary>
    public async Task<bool> ChangePasswordAsync(string userName, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userName, cancellationToken);
        if (user is null)
            return false;

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.SecurityStamp++;
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Changed password for admin account {UserName}", userName);
        return true;
    }
}
