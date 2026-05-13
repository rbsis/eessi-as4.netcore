namespace Eu.EDelivery.AS4.Services.DynamicDiscovery;

public interface IDynamicDiscoveryProfileResolver
{
    bool CanResolve(string? smpProfile);

    IDynamicDiscoveryProfile Resolve(string? smpProfile);
}
