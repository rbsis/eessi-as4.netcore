using System.ComponentModel;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;

namespace Eu.EDelivery.AS4.Steps.Send;

/// <summary>
/// Describes how a MessageUnit should be selected to be sent via Pulling.
/// </summary>
/// <seealso cref="IStep" />
[Info("Select message to send")]
[Description(
    "Selects a message that is eligible for sending via pulling. " +
    "This step selects a message that matches the MPC of the received pull-request signalmessage.")]
public class SelectUserMessageToSendStep : IStep
{
    private readonly IDatastoreRepository _repository;
    private readonly IAS4MessageBodyStore _messageBodyStore;
    private readonly IIdentifierFactory _identifierFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectUserMessageToSendStep" /> class.
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="messageBodyStore"></param>
    /// <param name="identifierFactory"></param>
    public SelectUserMessageToSendStep(
        IDatastoreRepository repository,
        IAS4MessageBodyStore messageBodyStore,
        IIdentifierFactory identifierFactory)
    {
        _messageBodyStore = messageBodyStore;
        _repository = repository;
        _identifierFactory = identifierFactory;
    }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext" />.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        var pullRequest = messagingContext.AS4Message?.FirstSignalMessage as PullRequest ?? throw new InvalidMessageException(
                "The received message is not a PullRequest message, " +
                "therefore no UserMessage can be selected to return to the sender");
        var match = _repository.RetrieveUserMessageForPullRequest(pullRequest);
        if (match is not null && match.ContentType is not null)
        {
            // Retrieve the existing MessageBody and put that stream in the MessagingContext.
            // The HttpReceiver processor will make sure that it gets serialized to the http response stream.
            var messageBody = await _messageBodyStore.LoadMessageBodyAsync(match.MessageLocation, cancellation);
            if (messageBody is null)
            {
                return await StepResult.FailedAsync(messagingContext);
            }

            messagingContext.ModifyContext(
                new ReceivedMessage(messageBody, match.ContentType),
                MessagingContextMode.Send);

            messagingContext.SendingPMode = await AS4XmlSerializer.FromStringAsync<SendingProcessingMode>(match.PMode, cancellation);

            return await StepResult.SuccessAsync(messagingContext);
        }

        var pullRequestWarning = AS4Message.Create(Error.CreatePullRequestWarning(_identifierFactory.Create()));
        messagingContext.ModifyContext(pullRequestWarning);

        return (await StepResult.SuccessAsync(messagingContext)).AndStopExecution();
    }
}
