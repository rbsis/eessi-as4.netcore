using Eu.EDelivery.AS4.Fe.Modules;

namespace Eu.EDelivery.AS4.Fe.Authentication;

/// <summary>
/// Setup authentication
/// </summary>
/// <seealso cref="IModular" />
/// <seealso cref="IRunAtServicesStartup" />
/// <seealso cref="IRunAtAppConfiguration" />
public interface IAuthenticationSetup : IModular, IRunAtServicesStartup, IRunAtAppConfiguration
{
}
