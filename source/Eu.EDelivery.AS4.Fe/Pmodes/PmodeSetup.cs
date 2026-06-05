using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Fe.Settings;

namespace Eu.EDelivery.AS4.Fe.Pmodes;

public class PmodeSetup : IPmodeSetup
{
    public void Run(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PmodeSettings>(configuration.GetSection("Pmodes"));

        services.AddScoped<IAs4PmodeSource, As4PmodeSource>();
        services.AddScoped<IPmodeService, PmodeService>();
    }
}