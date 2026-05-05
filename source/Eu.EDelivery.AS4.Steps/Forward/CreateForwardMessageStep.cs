using System.ComponentModel;
using Eu.EDelivery.AS4.Builders.Entities;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Forward;

[Info("Creates a copy of the received message so that it can be forwarded.")]
[Description("Creates a copy of the received message so that it can be forwarded.")]
public class CreateForwardMessageStep : IStep
{
    private readonly ILogger<CreateForwardMessageStep> _logger;
    private readonly IConfig _configuration;
    private readonly IAS4MessageBodyStore _bodyStore;
    private readonly IDatastoreRepository _repository;
    private readonly ISerializerProvider _serializerProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateForwardMessageStep" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="configuration">The local configuration.</param>
    /// <param name="bodyStore">The store where the datastore persist its messages.</param>
    /// <param name="repository"></param>
    /// <param name="serializerProvider"></param>
    public CreateForwardMessageStep(
        ILogger<CreateForwardMessageStep> logger,
        IConfig configuration,
        IAS4MessageBodyStore bodyStore,
        IDatastoreRepository repository,
        ISerializerProvider serializerProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _bodyStore = bodyStore;
        _repository = repository;
        _serializerProvider = serializerProvider;
    }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext"/>.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        var entityMessage = messagingContext.ReceivedMessage as ReceivedEntityMessage;
        if (entityMessage?.Entity is not InMessage receivedInMessage)
        {
            throw new InvalidOperationException(
                "The MessagingContext must contain a ReceivedMessage that represents an InMessage." + Environment.NewLine +
                "Other types of ReceivedMessage models are not supported in this Step.");
        }

        if (receivedInMessage.ContentType is null)
        {
            throw new InvalidOperationException("The ReceivedMessage must contain a ContentType.");
        }

        // Forward message by creating an OutMessage and set operation to 'ToBeProcessed'.
        _logger.LogInformation("{LogTag} Create a message that will be forwarded to the next MSH", messagingContext.LogTag);
        using var originalInMessage = await _bodyStore.LoadMessageBodyAsync(receivedInMessage.MessageLocation, cancellation)
            ?? throw new InvalidOperationException($"The ReceivedMessage was not found at location {receivedInMessage.MessageLocation}.");

        var outLocation = await _bodyStore.SaveAS4MessageStreamAsync(
            _configuration.OutMessageStoreLocation,
            originalInMessage,
            cancellation);

        originalInMessage.Position = 0;

        var msg = await _serializerProvider
            .Get(receivedInMessage.ContentType)
            .DeserializeAsync(originalInMessage, receivedInMessage.ContentType, cancellation)
            ?? throw new InvalidOperationException("The ReceivedMessage was not deserialized.");

        if (msg.PrimaryMessageUnit is null)
        {
            throw new InvalidOperationException("The ReceivedMessage does not have a PrimaryMessageUnit.");
        }

        // Only create an OutMessage for the primary message-unit.
        var outMessage = OutMessageBuilder
            .ForMessageUnit(
                msg.PrimaryMessageUnit,
                receivedInMessage.ContentType,
                messagingContext.SendingPMode)
            .BuildForForwarding(outLocation, receivedInMessage);

        _logger.LogDebug("Insert OutMessage {{Intermediary=true, Operation=ToBeProcesed}}");
        _repository.InsertOutMessage(outMessage);

        // Set the InMessage to Forwarded.
        // We do this for all InMessages that are present in this AS4 Message
        _repository.UpdateInMessages(
            m => msg.MessageIds.Contains(m.EbmsMessageId),
            r => r.Operation = Operation.Forwarded);

        return await StepResult.SuccessAsync(messagingContext);
    }
}
