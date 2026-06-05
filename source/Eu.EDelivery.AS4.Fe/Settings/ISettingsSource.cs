using Eu.EDelivery.AS4.Fe.Modules;

namespace Eu.EDelivery.AS4.Fe.Settings;

public interface ISettingsSource : IModular
{
    Task<Model.Internal.Settings?> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(Model.Internal.Settings settings, CancellationToken cancellationToken);
}
