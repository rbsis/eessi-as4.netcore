using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Services.DynamicDiscovery;

internal class DynamicDiscoveryProfileResolver : IDynamicDiscoveryProfileResolver
{
    private readonly ILogger<DynamicDiscoveryProfileResolver> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DynamicDiscoveryProfileResolver(ILogger<DynamicDiscoveryProfileResolver> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public bool CanResolve(string? smpProfile)
    {
        if (string.IsNullOrWhiteSpace(smpProfile))
        {
            return false;
        }

        var type = Type.GetType(smpProfile, throwOnError: false);
        if (type == null)
        {
            return false;
        }

        return type.GetInterfaces().Any(i => i == typeof(IDynamicDiscoveryProfile));
    }

    public IDynamicDiscoveryProfile Resolve(string? smpProfile)
    {
        if (string.IsNullOrWhiteSpace(smpProfile))
        {
            _logger.LogDebug("SendingPMode doesn't specify DynamicDiscovery.SmpProfile element, using default: {Profile}", nameof(LocalDynamicDiscoveryProfile));
            return _serviceProvider.GetRequiredService<LocalDynamicDiscoveryProfile>();
        }

        var type = Type.GetType(smpProfile, throwOnError: false);
        if (type == null)
        {
            _logger.LogError("SendingPMode element doesn't have a fully-qualified assembly name "
                + "that can be used to resolve a instance that implements the {Profile} interface, resolve using: {SmpProfile}",
                nameof(IDynamicDiscoveryProfile),
                smpProfile);

            throw new InvalidOperationException("Dynamic Discovery process was not correctly configured");
        }

        _logger.LogDebug("SendingPMode specifies a DynamicDiscovery.SmpProfile element, resolve using: {SmpProfile}", smpProfile);
        return _serviceProvider.GetService(type) as IDynamicDiscoveryProfile
            ?? throw new InvalidOperationException($"Cannot resolve a valid {nameof(IDynamicDiscoveryProfile)} implementation for the {smpProfile} fully-qualified assembly name");
    }
}
