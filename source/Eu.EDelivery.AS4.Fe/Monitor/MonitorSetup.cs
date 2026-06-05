using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Fe.Database;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Fe.Settings;
using Eu.EDelivery.AS4.Repositories;

namespace Eu.EDelivery.AS4.Fe.Monitor;

/// <summary>
/// Setup monitor
/// </summary>
/// <seealso cref="IMonitorSetup" />
public class MonitorSetup : IMonitorSetup
{
    private const string Section = "Monitor";

    /// <summary>
    /// Runs the specified services.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configuration">The configuration.</param>
    public void Run(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MonitorSettings>(configuration.GetSection(Section));

        var settings = configuration.GetRequiredSection(Section).Get<MonitorSettings>()
           ?? throw new InvalidOperationException("MonitorSettings settings not found.");

        services.AddDbContextFactory<DatastoreContext>(options => SqlConnectionBuilder.Build(settings.Provider, settings.ConnectionString, options));
        services.AddScoped<IMonitorService, MonitorService>();
        services.AddSingleton<IDatastoreRepository, DatastoreRepository>();
        services.AddSingleton<IClient, Client>();
    }
}
