using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Receivers;

/// <summary>
/// Builder to make <see cref="IReceiver"/> implementations
/// from <see cref="Receiver"/> settings
/// </summary>
internal class ReceiverBuilder : IReceiverBuilder
{
    private readonly ILogger<ReceiverBuilder> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ReceiverBuilder(ILogger<ReceiverBuilder> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a <see cref="IReceiver"/> implementation based on the given <paramref name="config"/>.
    /// </summary>
    /// <param name="config">The configuration which contains the type and the optional settings for the <see cref="IReceiver"/> implementation.</param>
    /// <returns></returns>
    public IReceiver BuildFromConfig(Receiver config)
    {
        if (string.IsNullOrWhiteSpace(config.Type))
        {
            _logger.LogError("Cannot resolve type string: {TypeString} to a {Name} instance because the type string is blank",
                config.Type,
                typeof(IReceiver).Name);

            throw new InvalidOperationException($"Cannot resolve type string: {config.Type} to a {typeof(IReceiver).Name} instance because the type string is blank");
        }

        var type = Type.GetType(config.Type, throwOnError: false);
        if (type == null)
        {
            _logger.LogError("Cannot resolve type string: {TypeString} to a {Name} instance because the type is not found in this AppDomain",
                config.Type,
                typeof(IReceiver).Name);

            throw new InvalidOperationException($"Cannot resolve type string: {config.Type} to a {typeof(IReceiver).Name} instance because the type is not found in this AppDomain");
        }

        var receiver = _serviceProvider.GetService(type) as IReceiver ??
            throw new InvalidOperationException($"Cannot resolve a valid {nameof(IReceiver)} implementation for the {config.Type} fully-qualified assembly name");

        if (config.Setting != null)
        {
            receiver.Configure(config.Setting);
        }

        return receiver;
    }
}
