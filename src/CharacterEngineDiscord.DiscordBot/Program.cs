using CharacterEngineDiscord.Core.Extensions;
using CharacterEngineDiscord.DataAccess.Extensions;
using CharacterEngineDiscord.DiscordBot.Extensions;
using CharacterEngineDiscord.Messaging.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

var settingsDir = Path.Combine(AppContext.BaseDirectory, "Settings");

builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile(Path.Combine(settingsDir, "appsettings.json"), optional: false, reloadOnChange: false);
builder.Configuration.AddJsonFile(Path.Combine(settingsDir, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true,
}));

builder.Logging.ClearProviders();
builder.Services.AddSerilog((sp, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(sp));

builder.Services.AddCharacterEngineCore(builder.Configuration);
builder.Services.AddCharacterEngineDataAccess(builder.Configuration);
builder.Services.AddCharacterEngineMessaging(builder.Configuration);
builder.Services.AddCharacterEngineDiscordBot(builder.Configuration);
builder.Services.AddCommandConsumer();

var host = builder.Build();
await host.RunAsync();
