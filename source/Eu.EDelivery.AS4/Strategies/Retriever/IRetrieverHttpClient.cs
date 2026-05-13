namespace Eu.EDelivery.AS4.Strategies.Retriever;

public interface IRetrieverHttpClient
{
    Task<HttpResponseMessage> GetPayloadAsync(string url, CancellationToken cancellation);
}
