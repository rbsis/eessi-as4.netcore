
using Eu.EDelivery.AS4.Strategies.Retriever;

namespace Eu.EDelivery.AS4.TestUtils.Stubs;

public class StubPayloadRetriever : IPayloadRetriever
{
    public static readonly StubPayloadRetriever Instance = new();

    private StubPayloadRetriever() { }

    public async Task<Stream> RetrievePayloadAsync(string location, CancellationToken cancellation)
    {
        return await Task.FromResult(Stream.Null);
    }
}
