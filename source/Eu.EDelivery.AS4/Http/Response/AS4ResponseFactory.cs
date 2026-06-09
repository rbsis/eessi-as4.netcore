using System.Net;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Streaming;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Http.Response;

public class AS4ResponseFactory : IAS4ResponseFactory
{
    private readonly ILogger<AS4ResponseFactory> _logger;
    private readonly ISerializerProvider _serializerProvider;

    public AS4ResponseFactory(ILogger<AS4ResponseFactory> logger, ISerializerProvider serializerProvider)
    {
        _logger = logger;
        _serializerProvider = serializerProvider;
    }

    public async Task<IAS4Response> Create(MessagingContext requestMessage, HttpWebResponse webResponse, CancellationToken cancellation)
    {
        var contentLength = webResponse.ContentLength;

        var contentStream = VirtualStream.Create(contentLength, forAsync: true);
        var receivedMessage = new ReceivedMessage(
            contentStream,
            webResponse.ContentType,
            webResponse.ResponseUri?.AbsolutePath ?? "unknown",
            contentLength);

        var receivedAS4Message = await TryDeserializeReceivedStream(receivedMessage, cancellation);
        if (!requestMessage.AS4Message?.IsEmpty == true && !receivedAS4Message.IsEmpty)
        {
            LogReceivedAS4Response(requestMessage.AS4Message!, receivedAS4Message);
        }

        var responseStream = webResponse.GetResponseStream() ?? Stream.Null;

        await responseStream.CopyToAsync(contentStream, cancellation);
        contentStream.Position = 0;

        return new AS4Response(webResponse.StatusCode, requestMessage, receivedMessage, receivedAS4Message);
    }

    public async Task<IAS4Response> CreateAsync(MessagingContext requestMessage, HttpResponseMessage responseMessage, CancellationToken cancellation)
    {
        var contentLength = responseMessage.Content.Headers.ContentLength ?? -1;

        var contentStream = VirtualStream.Create(contentLength, forAsync: true);
        await responseMessage.Content.CopyToAsync(contentStream, cancellation);
        contentStream.Position = 0;

        var receivedMessage = new ReceivedMessage(
            contentStream,
            responseMessage.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
            responseMessage.Headers.Location?.AbsolutePath ?? "unknown",
            contentLength);

        var receivedAS4Message = await TryDeserializeReceivedStream(receivedMessage, cancellation);
        if (!requestMessage.AS4Message?.IsEmpty == true && !receivedAS4Message.IsEmpty)
        {
            LogReceivedAS4Response(requestMessage.AS4Message!, receivedAS4Message);
        }

        return new AS4Response(responseMessage.StatusCode, requestMessage, receivedMessage, receivedAS4Message);
    }

    private void LogReceivedAS4Response(AS4Message request, AS4Message response)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }
        if (request?.PrimaryMessageUnit is not null && response.PrimaryMessageUnit is not null)
        {
            _logger.LogInformation("Sending AS4Message {PrimaryMessageId} results in: {RequestPrimaryMessageUnit} -> {ResponsePrimaryMessageUnit} ",
                request.GetPrimaryMessageId(),
                request.PrimaryMessageUnit.GetType().Name,
                response.PrimaryMessageUnit.GetType().Name);
        }

        foreach (var mu in response.MessageUnits)
        {
            switch (mu)
            {
                case Error err:
                    {
                        var message = $"Error message {err.FormatErrorLines()} "
                            + "Receipt message response received for message with ebMS Id {RefToMessageId}";
                        _logger.LogDebug(message, mu.RefToMessageId);
                    }
                    break;
                case Receipt r:
                    {
                        var message = $"{(r.NonRepudiationInformation is not null ? "Non-Repudiation " : string.Empty)}"
                            + "Receipt message response received for message with ebMS Id {RefToMessageId}";
                        _logger.LogDebug(message, mu.RefToMessageId);
                    }
                    break;
            }
        }
    }

    private async Task<AS4Message> TryDeserializeReceivedStream(ReceivedMessage receivedMessage, CancellationToken cancellation)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(receivedMessage.ContentType))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("No ContentType set - returning an empty AS4 response.");

                    // Not in 'using' because it closes the underlying stream
                    var streamReader = new StreamReader(receivedMessage.UnderlyingStream);
                    var responseContent = await streamReader.ReadToEndAsync(cancellation);
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        _logger.LogDebug(responseContent);
                    }
                }

                return AS4Message.Empty;
            }

            var serializer = _serializerProvider.Get(receivedMessage.ContentType);

            return await serializer.DeserializeAsync(receivedMessage.UnderlyingStream, receivedMessage.ContentType, cancellation);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Deserialize failed");
            return AS4Message.Empty;
        }
        finally
        {
            receivedMessage.UnderlyingStream.Position = 0;
        }
    }
}
