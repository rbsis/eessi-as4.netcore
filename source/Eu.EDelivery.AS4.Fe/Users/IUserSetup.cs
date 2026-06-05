using Eu.EDelivery.AS4.Fe.Modules;

namespace Eu.EDelivery.AS4.Fe.Users;

/// <summary>
/// Setup user module
/// </summary>
/// <seealso cref="IRunAtServicesStartup" />
public interface IUserSetup : IModular, IRunAtServicesStartup
{
}
