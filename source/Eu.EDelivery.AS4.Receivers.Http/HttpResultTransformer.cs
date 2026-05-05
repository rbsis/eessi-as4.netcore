using System.Net;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Receivers.Http;

internal class HttpResultTransformer : IHttpResultTransformer
{
    private readonly ILogger<HttpResultTransformer> _logger;
    private readonly ISerializerProvider _serializerProvider;

    public HttpResultTransformer(ILogger<HttpResultTransformer> logger, ISerializerProvider serializerProvider)
    {
        _logger = logger;
        _serializerProvider = serializerProvider;
    }

    /// <summary>
    /// Creates a new result based on an <see cref="AS4Message"/>.
    /// </summary>
    /// <param name="status"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public HttpResult FromAS4Message(HttpStatusCode status, AS4Message message)
    {
        return new HttpResult(
            status,
            message.ContentType,
            async response => await WriteAS4MessageToResponseAsync(message, response, CancellationToken.None));
    }

    private async Task WriteAS4MessageToResponseAsync(AS4Message message, HttpListenerResponse response, CancellationToken cancellation)
    {
        try
        {
            using var responseStream = response.OutputStream;
            if (!message.IsEmpty)
            {
                var serializer = _serializerProvider.Get(message.ContentType);

                await serializer.SerializeAsync(message, responseStream, cancellation);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, $"An error occured while writing the Response to the ResponseStream");
            throw;
        }
    }
}
