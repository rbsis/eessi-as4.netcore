using Eu.EDelivery.AS4.Fe.Services;

namespace Eu.EDelivery.AS4.Fe.SmpConfiguration;

/// <summary>
///     Implementation of <see cref="ISmpConfigurationSetup" />
/// </summary>
/// <seealso cref="ISmpConfigurationSetup" />
public class SmpConfigurationSetup : ISmpConfigurationSetup
{
    /// <summary>
    ///     Runs the specified services.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configuration">The configuration.</param>
    public void Run(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISmpConfigurationService, SmpConfigurationService>();
    }
}
