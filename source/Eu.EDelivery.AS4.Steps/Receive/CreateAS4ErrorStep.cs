using System.ComponentModel;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Microsoft.Extensions.Logging;
using Error = Eu.EDelivery.AS4.Model.Core.Error;
using PullRequest = Eu.EDelivery.AS4.Model.Core.PullRequest;
using SignalMessage = Eu.EDelivery.AS4.Model.Core.SignalMessage;
using UserMessage = Eu.EDelivery.AS4.Model.Core.UserMessage;

namespace Eu.EDelivery.AS4.Steps.Receive;

[Info("Create an AS4 Error message")]
[Description("Create an AS4 Error message to inform the sender that something went wrong processing the received AS4 message")]
public class CreateAS4ErrorStep : IStep
{
    private readonly ILogger<CreateAS4ErrorStep> _logger;
    private readonly IDatastoreRepository _repository;
    private readonly IIdentifierFactory _identifierFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAS4ErrorStep"/> class.
    /// </summary>
    public CreateAS4ErrorStep(ILogger<CreateAS4ErrorStep> logger, IDatastoreRepository repository, IIdentifierFactory identifierFactory)
    {
        _logger = logger;
        _identifierFactory = identifierFactory;
        _repository = repository;
    }

    /// <summary>
    /// Start creating <see cref="Error"/>
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <exception cref="System.Exception">A delegate callback throws an exception.</exception>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        var asS4Message = messagingContext.AS4Message;
        var errorResult = messagingContext.ErrorResult;

        if (asS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CreateAS4ErrorStep)} requires an AS4Message to create an Error but no AS4Message is present in the MessagingContext");
        }

        if (asS4Message.IsEmpty && errorResult == null)
        {
            _logger.LogWarning("Skip creating AS4 Error because AS4Message and ErrorResult is empty in the MessagingContext");
            return await StepResult.SuccessAsync(messagingContext);
        }

        var errorMessage = CreateAS4ErrorWithPossibleMultihop(
            received: asS4Message,
            occurredError: errorResult);

        if (errorResult != null)
        {
            _logger.LogError("AS4 Error(s) created with {Code} {Alias}, {Description}",
                errorResult.Code.GetString(),
                errorResult.Alias,
                errorResult.Description);

            await InsertInExceptionsForNowExceptionedInMessageAsync(
                asS4Message.SignalMessages,
                errorResult,
                messagingContext.ReceivingPMode,
                cancellation);
        }

        messagingContext.ModifyContext(errorMessage);

        if (_logger.IsEnabled(LogLevel.Information) && errorMessage.MessageUnits.Any())
        {
            _logger.LogInformation("{LogTag} {Count} Error(s) has been created for received AS4 UserMessages",
                messagingContext.LogTag,
                errorMessage.MessageUnits.Count());
        }

        return await StepResult.SuccessAsync(messagingContext);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1172:Unused method parameters should be removed", Justification = "<Pending>")]
    private AS4Message CreateAS4ErrorWithPossibleMultihop(
        AS4Message received,
        ErrorResult? occurredError)
    {
        Error ToError(UserMessage u) => Error.CreateFor(_identifierFactory.Create(), u, occurredError, received?.IsMultiHopMessage == true);

        var errors = received.UserMessages.Select(ToError) ?? [];
        var errorMessage = AS4Message.Create(errors);
        errorMessage.SigningId = received.SigningId;

        return errorMessage;
    }

    private async Task InsertInExceptionsForNowExceptionedInMessageAsync(
        IEnumerable<SignalMessage> signalMessages,
        ErrorResult occurredError,
        ReceivingProcessingMode? receivePMode,
        CancellationToken cancellation)
    {
        if (!signalMessages.Any())
        {
            return;
        }

        //TODO: transaction
        foreach (var signal in signalMessages.Where(s => s is not PullRequest))
        {
            var ex = InException.ForEbmsMessageId(signal.MessageId, occurredError.Description);
            await ex.SetPModeInformationAsync(receivePMode, cancellation);

            _logger.LogDebug("Insert InException for {Signal} {MessageId} with {{Exception={Description}}}",
                signal.GetType().Name,
                signal.MessageId,
                occurredError.Description);

            _repository.InsertInException(ex);
        }

        IEnumerable<string> ebmsMessageIds = signalMessages.Select(s => s.MessageId).ToArray();
        _repository.UpdateInMessages(
            m => ebmsMessageIds.Contains(m.EbmsMessageId),
            m =>
            {
                _logger.LogDebug("Update {EbmsMessageType} InMessage {EbmsMessageId} Status=Exception", m.EbmsMessageType, m.EbmsMessageId);
                m.SetStatus(InStatus.Exception);
            });
    }
}
