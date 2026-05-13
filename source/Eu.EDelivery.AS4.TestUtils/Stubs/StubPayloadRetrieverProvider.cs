
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Strategies.Retriever;

namespace Eu.EDelivery.AS4.TestUtils.Stubs;

public class StubPayloadRetrieverProvider : IPayloadRetrieverProvider
{
    public static readonly StubPayloadRetrieverProvider Instance = new();

    private StubPayloadRetrieverProvider() { }

    public IPayloadRetriever Get(Payload payload)
    {
        return StubPayloadRetriever.Instance;
    }
}
