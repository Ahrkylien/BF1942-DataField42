using System.Text.Json.Serialization;
using DataField42.Website.Configuration;
using DataField42.Website.Services;
using DataField42.Website.Services.ClientServer;
using DataField42.Website.Services.ServerQuery;
using DataField42.Website.Services.Tracking;

using Microsoft.Extensions.Options;

namespace DataField42.Website.Startup;

/// <summary>
/// Registers what the public pages are built from.
/// </summary>
public static class WebsiteServiceExtensions
{
    public static IServiceCollection AddWebsiteServices(this IServiceCollection services)
    {
        services.AddControllersWithViews()
            // The admin page reads job states by name, not by their position in the enum.
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddMemoryCache();
        services.AddHttpClient<MasterServerClient>(client => client.Timeout = TimeSpan.FromSeconds(10));

        // The DataField42 clients and servers, which connect on a port of their own.
        services.AddSingleton(provider => new ChecksumRepository(Path.Combine(provider.GetRequiredService<IOptions<WebsiteOptions>>().Value.FileTransferDirectory, "checksums.tsv")));
        services.AddSingleton<HeartbeatRegistry>();
        services.AddSingleton<ReleaseFiles>();
        services.AddScoped<ClientFileSelector>();
        services.AddScoped<DownloadHandler>();
        services.AddScoped<ClientRequestHandler>();
        services.AddHostedService<ClientServerHost>();

        // The servers tracker, which records round history from the master server.
        services.AddHttpClient<TrackerFeedReader>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<TrackingRecorder>();
        services.AddHostedService<ServerTrackerService>();

        services.AddSingleton<ClanTagService>();
        services.AddSingleton<ReferencePageStore>();
        services.AddSingleton<DiskSpaceReader>();

        services.AddScoped<Scoreboard>();
        services.AddScoped<FileDatabase>();
        services.AddScoped<MapPresenter>();
        services.AddScoped<PlayerActivityStatistics>();
        services.AddScoped<ServerListService>();
        services.AddScoped<DatabaseIntegrityService>();

        return services;
    }
}
