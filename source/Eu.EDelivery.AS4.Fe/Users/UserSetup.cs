using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Services;

namespace Eu.EDelivery.AS4.Fe.Users;

/// <summary>
/// Implementation of the user setup module
/// </summary>
/// <seealso cref="IUserSetup" />
public class UserSetup : IUserSetup
{
    /// <summary>
    /// Runs the specified services.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configuration">The configuration.</param>
    public void Run(IServiceCollection services, IConfiguration configuration)
    {
        //TODO: connection string should be read from configuration
        services.AddDbContextFactory<ApplicationDbContext>();
        services.AddScoped<IUserService, UserService>();
    }
}
