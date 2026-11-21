using DataField42.Website.Data;
using DataField42.Website.Services.Admin.Jobs;

namespace DataField42.Website.Startup;

/// <summary>
/// What happens once the application is built: preparing the admin table, and the request pipeline.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Creates the admin tables if this is the first run against a database that predates them,
    /// then settles what the last shutdown left in the job history. A failure here is logged
    /// rather than fatal: the public pages do not need any of it, and refusing to start the whole
    /// site over an admin-only feature would be worse.
    /// </summary>
    public static async Task PrepareAdminAreaAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataField42DbContext>>();

        try
        {
            var context = scope.ServiceProvider.GetRequiredService<DataField42DbContext>();
            await AdminSchema.EnsureCreatedAsync(context);
        }
        catch (Exception ex) when (DatabaseFailure.IsUnavailable(ex))
        {
            logger.LogError(ex, "Could not create the admin tables; the admin pages will not be usable");
            return;
        }

        await app.Services.GetRequiredService<JobRunner>().RestoreAsync();
    }

    public static WebApplication UseWebsitePipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            app.UseExceptionHandler("/Error");

        // Served under /static so every asset URL matches the paths the pages were written with.
        app.UseStaticFiles(new StaticFileOptions { RequestPath = "/static" });

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseStatusCodePagesWithReExecute("/NotFound", "?code={0}");
        app.MapControllers();

        return app;
    }
}
