using System.ComponentModel;
using System.Configuration;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Strategies.Retriever;
using MimeKit;

namespace Eu.EDelivery.AS4.Transformers;

/// <summary>
/// This Transformer is responsible for creating a <see cref="MessagingContext"/> that contains a <see cref="SubmitMessage"/> for the payload it has received. 
/// </summary>
/// <seealso cref="ITransformer" />
public class SubmitPayloadTransformer : ITransformer
{
    private readonly IConfig _config;
    private readonly IIdentifierFactory _identifierFactory;

    private IDictionary<string, string> _properties;

    public const string SendingPModeKey = "SendingPMode";

    [Info("Sending Processing Mode", required: true, type: "sendingpmode")]
    [Description("Sending Processing Mode identifier to indicate which default Processing Mode should be used to create a default SubmitMessage during the transformation of a Payload to a SubmitMessage.")]
    private string SendingPMode => _properties.ReadOptionalProperty(SendingPModeKey);

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitPayloadTransformer" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="identifierFactory"></param>
    public SubmitPayloadTransformer(IConfig configuration, IIdentifierFactory identifierFactory)
    {
        _config = configuration;
        _identifierFactory = identifierFactory;
        _properties = new Dictionary<string, string>();
    }

    /// <summary>
    /// Configures the <see cref="ITransformer"/> implementation with specific user-defined properties.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void Configure(IDictionary<string, string> properties) => _properties = properties;

    /// <summary>
    /// Transform a given <see cref="ReceivedMessage"/> to a Canonical <see cref="MessagingContext"/> instance.
    /// </summary>
    /// <param name="message">Given message to transform.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        var sendingPMode = GetSendingPMode();

        var payload = GetPayloadInfo(message);

        var submit = new SubmitMessage
        {
            MessageInfo =
            {
                MessageId = message.MessageId ?? _identifierFactory.Create(),
                RefToMessageId = message.RefToMessageId
            },
            Collaboration =
            {
                AgreementRef = sendingPMode != null ? new() { PModeId = sendingPMode.Id } : null,
                ConversationId = message.ConversationId
            },
            Payloads = [payload]
        };

        if (!string.IsNullOrEmpty(message.Type))
        {
            submit.MessageProperties = [new("Type", message.Type)];
        }

        return Task.FromResult(new MessagingContext(submit));
    }

    private SendingProcessingMode? GetSendingPMode()
    {
        if (string.IsNullOrEmpty(SendingPMode))
        {
            return null;
        }

        return _config.GetSendingPMode(id: SendingPMode) ??
            throw new ConfigurationErrorsException($"No Sending Processing Mode found for {SendingPMode}.");
    }

    private static Payload GetPayloadInfo(ReceivedMessage incoming)
    {
        if (incoming.UnderlyingStream is FileStream file)
        {
            var payloadPath = file.Name;

            return new()
            {
                Id = Path.GetFileNameWithoutExtension(new FileInfo(payloadPath).Name),
                MimeType = incoming.ContentType,
                Location = FilePayloadRetriever.Key + payloadPath,
                PayloadProperties = [new("MimeType", incoming.ContentType)]
            };
        }
        else
        {
            _ = MimeTypes.TryGetExtension(incoming.ContentType, out var extension);

            var payloadId = Guid.NewGuid().ToString();
            var payloadPath = Path.Combine(Path.GetTempPath(), payloadId + extension);

            using var tempStream = new FileStream(payloadPath, FileMode.Create, FileAccess.Write);
            incoming.UnderlyingStream.CopyTo(tempStream);

            return new()
            {
                Id = payloadId,
                MimeType = incoming.ContentType,
                Location = TempFilePayloadRetriever.Key + payloadPath,
                PayloadProperties = [new("MimeType", incoming.ContentType)]
            };
        }
    }
}
