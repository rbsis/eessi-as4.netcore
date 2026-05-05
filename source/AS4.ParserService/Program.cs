using AS4.ParserService;
using NLog;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("./bin/appsettings.payloadservice.json", true)
    .AddJsonFile("./appsettings.payloadservice.json", true)
    .AddEnvironmentVariables();

builder.Host
    .UseContentRoot(Directory.GetCurrentDirectory());

//builder.WebHost
//    .UseUrls("http://localhost:3000");

// NLog: Setup NLog for Dependency injection
LogManager.Setup().LoadConfiguration(builder =>
{
    builder.ForLogger().FilterMinLevel(NLog.LogLevel.Debug).WriteToConsole();
});

builder.Logging.ClearProviders();
builder.Host.UseNLog();

var startup = new Startup(builder.Configuration);

startup.ConfigureServices(builder.Services);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

await app.RunAsync();
