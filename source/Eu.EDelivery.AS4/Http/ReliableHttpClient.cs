using System.Net.Http.Headers;
using Eu.EDelivery.AS4.Http.Response;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Eu.EDelivery.AS4.Http;

internal class ReliableHttpClient : HttpClientBase, IReliableHttpClient
{
    private readonly ISerializerProvider _serializerProvider;
    private readonly IAS4ResponseFactory _responseFactory;

    public ReliableHttpClient(
        ILogger<ReliableHttpClient> logger,
        ISerializerProvider serializerProvider,
        IAS4ResponseFactory responseFactory) : base(logger)
    {
        _serializerProvider = serializerProvider;
        _responseFactory = responseFactory;
    }

    public IHttpRequest CreateRequest(string url, string contentType) => new AS4HttpRequest(url, contentType);

    public async Task<IAS4Response> PostRequestAsync(IHttpRequest request, MessagingContext ctx, CancellationToken cancellation)
    {
        var requestImplementation = request as AS4HttpRequest
            ?? throw new ArgumentException("Request is not an AS4HttpRequest", nameof(request));

        var content = await CreateStreamContentAsync(ctx, requestImplementation, cancellation);

        using var response = await PostRequestAsync(
            requestImplementation.Url,
            content,
            requestImplementation.Certificate,
            cancellation);

        return await _responseFactory.CreateAsync(ctx, response, cancellation);
    }

    private async Task<HttpContent> CreateStreamContentAsync(MessagingContext ctx, AS4HttpRequest requestImplementation, CancellationToken cancellation)
    {
        if (ctx.ReceivedMessage != null)
        {
            var content = new StreamContent(ctx.ReceivedMessage.UnderlyingStream);
            content.Headers.ContentType = new(ctx.ReceivedMessage.ContentType);
            return content;
        }

        if (requestImplementation.ContentType != null)
        {
            var requestStream = new MemoryStream();
            await _serializerProvider
                .Get(requestImplementation.ContentType)
                .SerializeAsync(ctx.AS4Message!, requestStream, cancellation);
            requestStream.Position = 0;

            var contentType = ContentType.Parse(requestImplementation.ContentType);

            var content = new StreamContent(requestStream);
            content.Headers.ContentType = new(contentType.MimeType);
            foreach (var parameter in contentType.Parameters)
            {
                var headerValue = new NameValueHeaderValue(parameter.Name, parameter.Value);
                content.Headers.ContentType.Parameters.Add(headerValue);
            }
            return content;
        }

        throw new InvalidOperationException("Content not available");
    }
}
