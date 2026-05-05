using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Transformers;

/// <summary>
/// <see cref="ITransformer"/> implementation to transform
/// incoming Payloads to a <see cref="MessagingContext"/>
/// </summary>
public class PayloadTransformer : ITransformer
{
    private readonly ILogger<PayloadTransformer> _logger;

    public PayloadTransformer(ILogger<PayloadTransformer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configures the <see cref="ITransformer"/> implementation with specific user-defined properties.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void Configure(IDictionary<string, string> properties) { }

    /// <summary>
    /// Tranform the Payload(s)
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        var attachment = CreateAttachmentFromReceivedMessage(message);
        var as4Message = AS4Message.Empty;
        as4Message.AddAttachment(attachment);

        _logger.LogInformation("Transform the given Payload to a AS4 Attachment");
        return await Task.FromResult(new MessagingContext(as4Message, MessagingContextMode.Submit));
    }

    private static Attachment CreateAttachmentFromReceivedMessage(ReceivedMessage receivedMessage)
    {
        return new Attachment(
            receivedMessage.UnderlyingStream,
            receivedMessage.ContentType);
    }
}
