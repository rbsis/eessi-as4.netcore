using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Sender;

/// <summary>
/// Class to provide <see cref="IDeliverSender" /> implementations
/// based on a given condition
/// </summary>
internal class NotifySenderProvider : INotifySenderProvider
{
    private readonly ILogger<NotifySenderProvider> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifySenderProvider" /> class.
    /// Create a new <see cref="NotifySenderProvider" />
    /// to select the provide the right <see cref="INotifySender" /> implementation
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S6672:Generic logger injection should match enclosing type", Justification = "<Pending>")]
    public NotifySenderProvider(ILogger<NotifySenderProvider> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Get the right <see cref="INotifySender" /> implementation
    /// for a given <paramref name="notifyMethod" />
    /// </summary>
    /// <param name="notifyMethod"></param>  
    /// <returns></returns>
    public INotifySender GetNotifySender(Method notifyMethod)
    {
        if (string.IsNullOrWhiteSpace(notifyMethod.Type))
        {
            _logger.LogError("Cannot resolve type string: {TypeString} to a {Name} instance because the type string is blank",
                notifyMethod.Type,
                typeof(INotifySender).Name);

            throw new InvalidOperationException($"Cannot resolve type string: {notifyMethod.Type} to a {typeof(INotifySender).Name} instance because the type string is blank");
        }

        var sender = _serviceProvider.GetKeyedService<INotifySender>(notifyMethod.Type) ??
            throw new InvalidOperationException($"Cannot resolve a valid {nameof(INotifySender)} implementation for key {notifyMethod.Type}");

        sender.Configure(notifyMethod);

        var logger = _serviceProvider.GetRequiredService<ILogger<ReliableNotifySender>>();
        return new ReliableNotifySender(logger, sender);
    }
}
