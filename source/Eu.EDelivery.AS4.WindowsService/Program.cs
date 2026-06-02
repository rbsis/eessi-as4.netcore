using Eu.EDelivery.AS4.WindowsService;
using NLog;
using NLog.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// NLog: Setup NLog for Dependency injection
LogManager.Setup().LoadConfiguration(builder =>
{
    builder.ForLogger().FilterMinLevel(NLog.LogLevel.Trace).WriteToColoredConsole();
});

builder.Logging.ClearProviders();
builder.Logging.AddNLog();

var startup = new Startup(builder.Configuration);

startup.ConfigureServices(builder.Services);

var host = builder.Build();

await host.RunAsync();
