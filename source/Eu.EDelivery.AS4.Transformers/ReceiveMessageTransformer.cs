using System.ComponentModel;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Streaming;
using Eu.EDelivery.AS4.Utilities;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Transformers;

public class ReceiveMessageTransformer : ITransformer
{
    private readonly ILogger<ReceiveMessageTransformer> _logger;
    private readonly IConfig _config;
    private readonly IIdentifierFactory _identifierFactory;
    private readonly ISerializerProvider _serializerProvider;

    private IDictionary<string, string> _properties;

    public const string ReceivingPModeKey = "ReceivingPMode";

    [Info("Receiving Processing Mode", required: false, type: "receivingpmode")]
    [Description("ReceivingPMode identifier that defines the PMode that must be used while processing a received AS4 Message")]
    private string ReceivingPMode => _properties.ReadOptionalProperty(ReceivingPModeKey);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReceiveMessageTransformer"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="identifierFactory"></param>
    /// <param name="serializerProvider"></param>
    public ReceiveMessageTransformer(
        ILogger<ReceiveMessageTransformer> logger,
        IConfig configuration,
        IIdentifierFactory identifierFactory,
        ISerializerProvider serializerProvider)
    {
        _logger = logger;
        _config = configuration;
        _identifierFactory = identifierFactory;
        _properties = new Dictionary<string, string>();
        _serializerProvider = serializerProvider;
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
    public async Task<MessagingContext> TransformAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        if (message.UnderlyingStream == null)
        {
            throw new InvalidMessageException(
                "The incoming stream is not an ebMS Message. " +
                "Only ebMS messages conform with the AS4 Profile are supported.");
        }

        if (!ContentTypeSupporter.IsContentTypeSupported(message.ContentType))
        {
            throw new InvalidMessageException(
                $"ContentType is not supported {message.ContentType}{Environment.NewLine}" +
                $"Supported ContentTypes are {Constants.ContentTypes.Soap} and {Constants.ContentTypes.Mime}");
        }

        var rm = await EnsureIncomingStreamIsSeekable(message, cancellation);
        var as4Message = await DeserializeToAS4MessageAsync(rm, cancellation);

        //Debug.Assert(m.UnderlyingStream.Position == 0, "The Deserializer failed to reposition the stream to its start-position");

        if (as4Message.IsSignalMessage && !string.IsNullOrEmpty(ReceivingPMode))
        {
            _logger.LogError(
                "Static Receive configuration doesn't allow receiving signal messages. " +
                "Please remove the static configured Receiving PMode: {ReceivingPMode} to also receive signal messages",
                ReceivingPMode);

            throw new InvalidMessageException(
                "Static Receive configuration doesn't allow receiving signal messages. ");
        }

        if (as4Message.PrimaryMessageUnit is not null)
        {
            _logger.LogInformation("(Receive) Receiving AS4Message -> {PrimaryMessageUnit} {MessageId}",
                as4Message.PrimaryMessageUnit.GetType().Name,
                as4Message.PrimaryMessageUnit.MessageId);
        }

        var context = new MessagingContext(as4Message, rm, MessagingContextMode.Receive);

        if (!string.IsNullOrEmpty(ReceivingPMode))
        {
            var pmode = _config.GetReceivingPModes().FirstOrDefault(p => p.Id == ReceivingPMode);

            if (pmode != null)
            {
                context.ReceivingPMode = pmode;
            }
            else
            {
                var errorMessage = "ReceivingPMode with Id: {ReceivingPMode} was configured as default PMode, but this PMode cannot be found in the configured receiving PModes."
                    + $"{Environment.NewLine} Configured Receiving PModes are placed on the folder: '.\\config\\receive-pmodes\\'.";
                _logger.LogError(errorMessage, ReceivingPMode);

                var errorResult = new ErrorResult(
                    "Static configured ReceivingPMode cannot be found",
                    ErrorAlias.ProcessingModeMismatch);

                var as4Error = new Error(
                    _identifierFactory.Create(),
                    as4Message.GetPrimaryMessageId() ?? _identifierFactory.Create(),
                    ErrorLine.FromErrorResult(errorResult));

                return new MessagingContext(AS4Message.Create(as4Error), MessagingContextMode.Receive)
                {
                    ErrorResult = errorResult
                };
            }
        }

        return context;
    }

    private static async Task<ReceivedMessage> EnsureIncomingStreamIsSeekable(ReceivedMessage m, CancellationToken cancellation)
    {
        if (m.UnderlyingStream.CanSeek)
        {
            return m;
        }

        var str = VirtualStream.Create(
            expectedSize: m.UnderlyingStream.CanSeek
                ? m.UnderlyingStream.Length
                : VirtualStream.ThresholdMax,
            forAsync: true);

        await m.UnderlyingStream.CopyToAsync(str, cancellation);
        str.Position = 0;

        return new ReceivedMessage(
            str,
            m.ContentType,
            m.Origin,
            m.Length);
    }

    private async Task<AS4Message> DeserializeToAS4MessageAsync(ReceivedMessage message, CancellationToken cancellation)
    {
        try
        {
            return await _serializerProvider
                .Get(message.ContentType)
                .DeserializeAsync(message.UnderlyingStream, message.ContentType, cancellation);
        }
        catch (Exception ex)
        {
            var errorMessage = "The incoming stream is not an ebMS Message, " +
                $"although the Content-Type is: {message.ContentType}. " +
                "Only ebMS messages conform with the AS4 Profile are supported.";
            _logger.LogError(ex, errorMessage);

            throw new InvalidMessageException(errorMessage);
        }
    }
}
