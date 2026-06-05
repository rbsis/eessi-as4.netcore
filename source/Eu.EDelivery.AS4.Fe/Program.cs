using Eu.EDelivery.AS4.Fe;
using NLog;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("./bin/appsettings.json", true)
    .AddJsonFile("appsettings.json", true, true)
#if DEBUG
    .AddJsonFile("appsettings.Development.json", true)
#endif
    .AddEnvironmentVariables();

// NLog: Setup NLog for Dependency injection
LogManager.Setup().LoadConfiguration(builder =>
{
    builder.ForLogger().FilterMinLevel(NLog.LogLevel.Debug).WriteToColoredConsole();
});

builder.Logging.ClearProviders();
builder.Host.UseNLog();

var startup = new Startup(builder.Configuration);

// Add services to the container.
startup.ConfigureServices(builder.Services);

var app = builder.Build();

startup.Configure(app);

await app.RunAsync();
