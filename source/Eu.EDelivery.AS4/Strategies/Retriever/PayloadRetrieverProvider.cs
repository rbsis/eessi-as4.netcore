using Microsoft.Extensions.DependencyInjection;

namespace Eu.EDelivery.AS4.Strategies.Retriever;

/// <summary>
/// Class to provide <see cref="IPayloadRetriever"/> implementations
/// </summary>
internal class PayloadRetrieverProvider : IPayloadRetrieverProvider
{
    private readonly ICollection<PayloadStrategyEntry> _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadRetrieverProvider"/> class. 
    /// Create a new Provider with empty <see cref="IPayloadRetriever"/> implementations
    /// </summary>
    public PayloadRetrieverProvider(
        [FromKeyedServices(FilePayloadRetriever.Key)] IPayloadRetriever filePayloadRetriever,
        [FromKeyedServices(TempFilePayloadRetriever.Key)] IPayloadRetriever tempFilePayloadRetriever,
        [FromKeyedServices(HttpPayloadRetriever.Key)] IPayloadRetriever httpPayloadRetriever)
    {
        _entries =
        [
            new(p => p.Location?.StartsWith(FilePayloadRetriever.Key, StringComparison.OrdinalIgnoreCase) == true, filePayloadRetriever),
            new(p => p.Location?.StartsWith(TempFilePayloadRetriever.Key, StringComparison.OrdinalIgnoreCase) == true, tempFilePayloadRetriever),
            new(p => p.Location?.StartsWith(HttpPayloadRetriever.Key, StringComparison.OrdinalIgnoreCase) == true, httpPayloadRetriever),
        ];
    }

    /// <summary>
    /// Get a specific Payload Retriever for a given Payload
    /// </summary>
    /// <param name="payload"></param>
    /// <returns></returns>
    public IPayloadRetriever Get(Model.Common.Payload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var entry = _entries.FirstOrDefault(e => e.Condition(payload));

        if (entry?.Retriever == null)
        {
            throw new KeyNotFoundException($"No {nameof(IPayloadRetriever)} implementation found for payload {payload.Id}");
        }

        return entry.Retriever;
    }

    /// <summary>
    /// Helper class to register the <see cref="IPayloadRetriever"/> implementation
    /// </summary>
    private sealed class PayloadStrategyEntry
    {
        public Func<Model.Common.Payload, bool> Condition { get; }
        public IPayloadRetriever Retriever { get; }

        public PayloadStrategyEntry(Func<Model.Common.Payload, bool> condition, IPayloadRetriever retriever)
        {
            Condition = condition;
            Retriever = retriever;
        }
    }
}
