using Eu.EDelivery.AS4.Http;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Retriever;

internal class RetrieverHttpClient : HttpClientBase, IRetrieverHttpClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RetrieverHttpClient(
        ILogger<RetrieverHttpClient> logger,
        IHttpClientFactory httpClientFactory) : base(logger)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HttpResponseMessage> GetPayloadAsync(string url, CancellationToken cancellation)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));

        var client = _httpClientFactory.CreateClient();
        return await client.SendAsync(request, cancellation);
    }
}
