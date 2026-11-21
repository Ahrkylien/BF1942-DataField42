using DataField42.Website.Configuration;

namespace DataField42.Website.Startup;

/// <summary>
/// Registers the website's own settings.
/// </summary>
public static class WebsiteOptionsExtensions
{
    /// <summary>
    /// Binds <see cref="WebsiteOptions"/> and checks it when the application starts, so a missing
    /// required setting stops the site with a message naming it instead of failing on first use.
    /// </summary>
    public static IServiceCollection AddWebsiteOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WebsiteOptions>()
            .Bind(configuration.GetSection(WebsiteOptions.SectionName))
            .PostConfigure<IHostEnvironment>((options, environment) => options.ResolveDirectories(environment.ContentRootPath))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
