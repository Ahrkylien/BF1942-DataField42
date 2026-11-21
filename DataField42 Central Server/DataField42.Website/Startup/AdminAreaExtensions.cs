using DataField42.Website.Data.Entities;
using DataField42.Website.Security;
using DataField42.Website.Services.Admin;
using DataField42.Website.Services.Admin.Jobs;
using DataField42.Website.Services.Admin.Update;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace DataField42.Website.Startup;

/// <summary>
/// Registers the admin pages: who may reach them, and what they can run.
/// </summary>
public static class AdminAreaExtensions
{
    public static IServiceCollection AddAdminArea(this IServiceCollection services)
    {
        services.AddSingleton<JobRunStore>();
        services.AddSingleton<JobRunner>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();

        services.AddScoped<AdminUserService>();
        services.AddScoped<FileTransferService>();

        // The admin page lists and starts jobs through this one interface, so a new job becomes
        // available by being registered here and nowhere else.
        services.AddScoped<IAdminJob, ModListJob>();
        services.AddScoped<IAdminJob, MapListJob>();
        services.AddScoped<IAdminJob, MapInfoJob>();
        services.AddScoped<IAdminJob, RfaInfoJob>();
        services.AddScoped<IAdminJob, TagsAndAuthorsJob>();
        services.AddScoped<IAdminJob, ServerFileListJob>();
        services.AddScoped<IAdminJob, ApplyTransferJob>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = AdminAuthentication.LoginPath;
                options.LogoutPath = AdminAuthentication.LogoutPath;
                options.AccessDeniedPath = AdminAuthentication.LoginPath;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                options.Cookie.Name = "df42.admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.EventsType = typeof(AdminCookieEvents);
            });
        services.AddScoped<AdminCookieEvents>();

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminAuthentication.PolicyName, policy => policy.RequireAuthenticatedUser());

        return services;
    }
}
