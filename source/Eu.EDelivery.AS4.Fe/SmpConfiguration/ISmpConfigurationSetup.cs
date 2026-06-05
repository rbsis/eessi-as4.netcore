using Eu.EDelivery.AS4.Fe.Modules;

namespace Eu.EDelivery.AS4.Fe.SmpConfiguration;

/// <summary>
///     Smp configuration module
/// </summary>
/// <seealso cref="IModular" />
/// <seealso cref="IRunAtServicesStartup" />
public interface ISmpConfigurationSetup : IModular, IRunAtServicesStartup
{
}
