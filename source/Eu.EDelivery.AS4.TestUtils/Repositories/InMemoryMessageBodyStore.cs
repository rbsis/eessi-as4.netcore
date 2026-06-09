using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;

namespace Eu.EDelivery.AS4.TestUtils.Repositories;

/// <summary>
/// In-Memory Implementation to store the <see cref="AS4Message"/> instances.
/// </summary>
/// <seealso cref="IAS4MessageBodyStore" />
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3881:\"IDisposable\" should be implemented correctly", Justification = "<Pending>")]
public class InMemoryMessageBodyStore : IAS4MessageBodyStore, IDisposable
{
    private readonly Dictionary<string, Stream> _store = [];

    private readonly ISerializerProvider _serializerProvider;

    public InMemoryMessageBodyStore(ISerializerProvider serializerProvider)
    {
        _serializerProvider = serializerProvider;
    }

    /// <summary>
    /// Loads a <see cref="Stream" /> at a given stored <paramref name="location" />.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<Stream?> LoadMessageBodyAsync(string? location, CancellationToken cancellation)
    {
        if (location == null || !_store.ContainsKey(location))
        {
            throw new InvalidOperationException($"MessageBodyStore does not contain an entry for {location}");
        }

        var messageStream = _store[location];
        messageStream.Position = 0;

        return await Task.FromResult(messageStream);
    }

    /// <summary>
    /// Saves a given <see cref="AS4Message" /> to a given location.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="message">The message to save.</param>
    /// <returns>
    /// Location where the <paramref name="message" /> is saved.
    /// </returns>
    public string SaveAS4Message(string location, AS4Message message)
    {
        var id = Guid.NewGuid().ToString();

        var serializer = _serializerProvider.Get(message.ContentType);
        var stream = new MemoryStream();
        serializer.Serialize(message, stream);

        _store.Add(id, stream);

        return id;
    }

    public Task<string> SaveAS4MessageStreamAsync(string location, Stream as4MessageStream, CancellationToken cancellation)
    {
        var locationId = Guid.NewGuid().ToString();

        _store.Add(locationId, as4MessageStream);

        return Task.FromResult(locationId);
    }


    /// <inheritdoc />
    public void UpdateAS4Message(string location, AS4Message message)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var kvp in _store)
        {
            kvp.Value.Dispose();
        }
        _store.Clear();
    }
}
