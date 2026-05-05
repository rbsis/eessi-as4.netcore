using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Transformers;

internal class TransformerBuilder : ITransformerBuilder
{
    private readonly ILogger<TransformerBuilder> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Prevents a default instance of the <see cref="TransformerBuilder"/> class from being created.
    /// </summary>
    public TransformerBuilder(ILogger<TransformerBuilder> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a <see cref="ITransformer"/> implementation based on the given <paramref name="config"/>.
    /// </summary>
    /// <param name="config">The configuration which contains the type and the optional settings for the <see cref="ITransformer"/> implementation.</param>
    /// <returns></returns>
    public ITransformer BuildFromConfig(Transformer config)
    {
        if (string.IsNullOrWhiteSpace(config.Type))
        {
            _logger.LogError("Cannot resolve type string: {TypeString} to a {Name} instance because the type string is blank",
                config.Type,
                typeof(ITransformer).Name);

            throw new InvalidOperationException($"Cannot resolve type string: {config.Type} to a {typeof(ITransformer).Name} instance because the type string is blank");
        }

        var type = Type.GetType(config.Type, throwOnError: false);
        if (type == null)
        {
            _logger.LogError("Cannot resolve type string: {TypeString} to a {Name} instance because the type is not found in this AppDomain",
                config.Type,
                typeof(ITransformer).Name);

            throw new InvalidOperationException($"Cannot resolve type string: {config.Type} to a {typeof(ITransformer).Name} instance because the type is not found in this AppDomain");
        }

        var transformer = _serviceProvider.GetService(type) as ITransformer ??
            throw new InvalidOperationException($"Cannot resolve a valid {nameof(ITransformer)} implementation for the {config.Type} fully-qualified assembly name");

        if (config.Setting != null)
        {
            transformer.Configure(config.Setting.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase));
        }

        return transformer;
    }
}
