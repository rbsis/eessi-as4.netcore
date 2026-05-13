using System.Xml;
using System.Xml.Schema;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Resources;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Transformers;

/// <summary>
/// Adapter to "Adapt" a SubmitMessage > AS4Message
/// </summary>
public class SubmitMessageXmlTransformer : ITransformer
{
    private readonly ILogger<SubmitMessageXmlTransformer> _logger;

    public SubmitMessageXmlTransformer(ILogger<SubmitMessageXmlTransformer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configures the <see cref="ITransformer"/> implementation with specific user-defined properties.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void Configure(IDictionary<string, string> properties) { }

    /// <summary>
    /// Transform a <see cref="SubmitMessage" />
    /// to a <see cref="MessagingContext"/>
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogTrace("Start deserializing to a SubmitMessage...");
        var submitMessage = DeserializeSubmitMessage(message);
        _logger.LogTrace("Successfully deserialized to a SubmitMessage");

        return await Task.FromResult(new MessagingContext(submitMessage));
    }

    private SubmitMessage DeserializeSubmitMessage(ReceivedMessage message)
    {
        try
        {
            var doc = new XmlDocument();
            doc.Load(message.UnderlyingStream);

            var schemas = new XmlSchemaSet();
            if (XsdSchemaDefinitions.SubmitMessage != null)
            {
                schemas.Add(XsdSchemaDefinitions.SubmitMessage);
            }
            doc.Schemas = schemas;

            doc.Validate((sender, args) =>
            {
                _logger.LogCritical("Incoming Submit Message doesn't match the XSD: {Message}", args.Message);
                throw args.Exception;
            });

            return AS4XmlSerializer.FromString<SubmitMessage>(doc.OuterXml)
                ?? throw new InvalidMessageException($"Received stream from {message.Origin} is not a SubmitMessage");
        }
        catch (Exception ex)
        {
            throw new InvalidMessageException($"Received stream from {message.Origin} is not a SubmitMessage", ex);
        }
    }
}
