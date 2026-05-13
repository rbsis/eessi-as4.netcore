using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Streaming;
using Eu.EDelivery.AS4.Utilities;

namespace Eu.EDelivery.AS4.Transformers;

/// <summary>
/// Transform <see cref="ReceivedMessage" />
/// to a <see cref="MessagingContext" /> with an <see cref="AS4Message" />
/// </summary>
public class AS4MessageTransformer : ITransformer
{
    private readonly ISerializerProvider _serializerProvider;

    public AS4MessageTransformer(ISerializerProvider serializerProvider)
    {
        _serializerProvider = serializerProvider;
    }

    /// <summary>
    /// Configures the <see cref="ITransformer"/> implementation with specific user-defined properties.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void Configure(IDictionary<string, string> properties) { }

    /// <summary>
    /// Transform to a <see cref="MessagingContext" />
    /// with a <see cref="AS4Message" /> included
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        if (message.UnderlyingStream == null)
        {
            throw new InvalidDataException($"The incoming stream from {message.Origin} is not an ebMS Message");
        }

        if (!ContentTypeSupporter.IsContentTypeSupported(message.ContentType))
        {
            throw new InvalidDataException($"ContentType {nameof(message.ContentType)} is not supported");
        }

        var messageStream = await CopyIncomingStreamToVirtualStream(message, cancellation);
        var as4Message = await DeserializeMessage(message.ContentType, messageStream, cancellation);

        return new MessagingContext(as4Message, message, MessagingContextMode.Unknown);
    }

    private static async Task<VirtualStream> CopyIncomingStreamToVirtualStream(ReceivedMessage receivedMessage, CancellationToken cancellation)
    {
        if (receivedMessage.UnderlyingStream is VirtualStream stream)
        {
            return stream;
        }

        var messageStream =
            VirtualStream.Create(
                receivedMessage.UnderlyingStream.CanSeek
                    ? receivedMessage.UnderlyingStream.Length
                    : VirtualStream.ThresholdMax,
                forAsync: true);

        await receivedMessage.UnderlyingStream.CopyToAsync(messageStream, cancellation);

        messageStream.Position = 0;

        return messageStream;
    }

    private async Task<AS4Message> DeserializeMessage(
        string contentType,
        Stream virtualStream,
        CancellationToken cancellation)
    {
        var serializer = _serializerProvider.Get(contentType);
        return await serializer.DeserializeAsync(virtualStream, contentType, cancellation);
    }
}
