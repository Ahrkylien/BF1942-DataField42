using DataField42.Website.Configuration;
using DataField42.Website.Startup;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

bool isSetupCommand = SetupCommands.IsSetupCommand(args);

builder.Configuration.AddLocalSettings(builder.Environment, args);
SetupCommands.ApplyArguments(builder.Configuration, args);

builder.Services
    .AddWebsiteOptions(builder.Configuration)
    .AddWebsiteServices()
    .AddAdminArea();

try
{
    builder.Services.AddDatabase(builder.Configuration);
}
catch (InvalidOperationException ex) when (isSetupCommand)
{
    // A setup command is run by a person, who wants the reason and not a stack trace.
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var app = builder.Build();

if (isSetupCommand)
    return await SetupCommands.RunAsync(app, args);

app.Logger.LogInformation("Database server version: {Version}", app.Services.GetRequiredService<ServerVersion>());

await app.PrepareAdminAreaAsync();
app.UseWebsitePipeline();
app.Run();

return 0;
