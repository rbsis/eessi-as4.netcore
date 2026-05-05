using Eu.EDelivery.AS4.Model.Core;

namespace Eu.EDelivery.AS4.Repositories;

public class AS4MessageStoreProvider : IAS4MessageBodyStore
{
    private readonly Dictionary<Func<string, bool>, IAS4MessageBodyStore> _stores = [];

    /// <summary>
    /// Accepts the specified condition.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="persister">The persister.</param>
    public void Accept(Func<string, bool> condition, IAS4MessageBodyStore persister) =>
        _stores[condition] = persister;

    /// <summary>
    /// Loads a <see cref="Stream" /> at a given stored <paramref name="location" />.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<Stream?> LoadMessageBodyAsync(string? location, CancellationToken cancellation) =>
        await For(location).LoadMessageBodyAsync(location, cancellation);

    /// <summary>
    /// Saves a given <see cref="AS4Message" /> to a given location.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="message">The message to save.</param>
    /// <returns>
    /// Location where the <paramref name="message" /> is saved.
    /// </returns>
    public string SaveAS4Message(string location, AS4Message message) =>
        For(location).SaveAS4Message(location, message);

    public async Task<string> SaveAS4MessageStreamAsync(string location, Stream as4MessageStream, CancellationToken cancellation) =>
        await For(location).SaveAS4MessageStreamAsync(location, as4MessageStream, cancellation);

    /// <summary>
    /// Updates an existing AS4 Message body.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="message">The message that should overwrite the existing messagebody.</param>
    /// <returns></returns>
    public void UpdateAS4Message(string location, AS4Message message) =>
        For(location).UpdateAS4Message(location, message);

    private IAS4MessageBodyStore For(string? key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var entry = _stores.FirstOrDefault(c => c.Key(key));
        return entry.Value ?? throw new KeyNotFoundException($"No registered {nameof(IAS4MessageBodyStore)} found for {key}");
    }
}
