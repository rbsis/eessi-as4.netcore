using System.Net;
using Eu.EDelivery.AS4.Http;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Notify;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Strategies.Sender;

internal class SenderHttpClient : HttpClientBase, ISenderHttpClient
{
    public SenderHttpClient(ILogger<SenderHttpClient> logger) : base(logger)
    {
    }

    public async Task<HttpStatusCode> PostDeliverMessageEnvelopeAsync(string url, DeliverMessageEnvelope envelop, CancellationToken cancellation) => await PostContentAsync(
        url,
        envelop.ContentType,
        envelop.SerializeMessage(),
        cancellation);

    public async Task<HttpStatusCode> PostNotifyMessageEnvelopeAsync(string url, NotifyMessageEnvelope envelop, CancellationToken cancellation) => await PostContentAsync(
        url,
        envelop.ContentType,
        envelop.NotifyMessage,
        cancellation);

    private async Task<HttpStatusCode> PostContentAsync(string url, string contentType, byte[] contents, CancellationToken cancellation)
    {
        var content = new ByteArrayContent(contents);
        content.Headers.ContentType = new(contentType);

        using var response = await PostRequestAsync(url, content, null, cancellation);
        return response.StatusCode;
    }
}
