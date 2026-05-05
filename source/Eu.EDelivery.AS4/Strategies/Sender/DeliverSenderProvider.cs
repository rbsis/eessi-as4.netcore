using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Sender;

/// <summary>
/// Class to provide <see cref="IDeliverSender" /> implementations
/// based on a given condition
/// </summary>
internal class DeliverSenderProvider : IDeliverSenderProvider
{
    private readonly ILogger<DeliverSenderProvider> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliverSenderProvider" /> class.
    /// Create a new <see cref="DeliverSenderProvider" />
    /// to select the provide the right <see cref="IDeliverSender" /> implementation
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S6672:Generic logger injection should match enclosing type", Justification = "<Pending>")]
    public DeliverSenderProvider(ILogger<DeliverSenderProvider> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Get the right <see cref="IDeliverSender" /> implementation
    /// for a given <paramref name="deliverMethod" />
    /// </summary>
    /// <param name="deliverMethod"></param>
    /// <returns></returns>
    /// <exception cref="Exception">A delegate callback throws an exception.</exception>
    public IDeliverSender GetDeliverSender(Method deliverMethod)
    {
        if (string.IsNullOrWhiteSpace(deliverMethod.Type))
        {
            _logger.LogError("Cannot resolve type string: {TypeString} to a {Name} instance because the type string is blank",
                deliverMethod.Type,
                typeof(IDeliverSender).Name);

            throw new InvalidOperationException($"Cannot resolve type string: {deliverMethod.Type} to a {typeof(IDeliverSender).Name} instance because the type string is blank");
        }

        var sender = _serviceProvider.GetKeyedService<IDeliverSender>(deliverMethod.Type) ??
            throw new InvalidOperationException($"Cannot resolve a valid {nameof(IDeliverSender)} implementation for key {deliverMethod.Type}");

        sender.Configure(deliverMethod);

        var logger = _serviceProvider.GetRequiredService<ILogger<ReliableDeliverSender>>();
        return new ReliableDeliverSender(logger, sender);
    }
}
