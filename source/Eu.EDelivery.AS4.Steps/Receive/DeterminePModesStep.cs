using System.ComponentModel;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Mappings.Core;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Steps.Receive.Participant;
using Eu.EDelivery.AS4.Xml;
using Microsoft.Extensions.Logging;
using PullRequest = Eu.EDelivery.AS4.Model.Core.PullRequest;
using ReceivePMode = Eu.EDelivery.AS4.Model.PMode.ReceivingProcessingMode;
using SendPMode = Eu.EDelivery.AS4.Model.PMode.SendingProcessingMode;
using SignalMessage = Eu.EDelivery.AS4.Model.Core.SignalMessage;
using UserMessage = Eu.EDelivery.AS4.Model.Core.UserMessage;

namespace Eu.EDelivery.AS4.Steps.Receive;

/// <summary>
/// Step which describes how the PModes (Sending and Receiving) is determined
/// </summary>
[Info("Determine PMode for received AS4 Message")]
[Description("Determines the PMode that must be used to process the received AS4 Message")]
public class DeterminePModesStep : IStep
{
    private readonly ILogger<DeterminePModesStep> _logger;
    private readonly IConfig _config;
    private readonly IDatastoreRepository _repository;
    private readonly IPModeRuleEngine _pmodeRuleEngine;
    /// <summary>
    /// Initializes a new instance of the <see cref="DeterminePModesStep"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="config">The configuration.</param>
    /// <param name="repository"></param>
    /// <param name="pmodeRuleEngine"></param>
    public DeterminePModesStep(
        ILogger<DeterminePModesStep> logger,
        IConfig config,
        IDatastoreRepository repository,
        IPModeRuleEngine pmodeRuleEngine)
    {
        _config = config;
        _logger = logger;
        _repository = repository;
        _pmodeRuleEngine = pmodeRuleEngine;
    }

    /// <summary>
    /// Start determine the Receiving Processing Mode
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        if (messagingContext.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(DeterminePModesStep)} requires an AS4Message but no AS4Message is present in the MessagingContext");
        }

        (var sendingPMode, var receivingPMode, var error) =
            DeterminePModes(messagingContext.AS4Message, messagingContext.SendingPMode, messagingContext.ReceivingPMode);

        messagingContext.SendingPMode = sendingPMode;
        messagingContext.ReceivingPMode = receivingPMode;
        messagingContext.ErrorResult = error;

        if (sendingPMode != null)
        {
            _logger.LogInformation("Determine SendingPMode \"{PModeId}\"", sendingPMode.Id);
        }

        if (receivingPMode != null)
        {
            _logger.LogInformation("Determine ReceivingPMode \"{PModeId}\"", receivingPMode.Id);
        }

        return error == null
            ? StepResult.SuccessAsync(messagingContext)
            : StepResult.FailedAsync(messagingContext);
    }

    private (SendPMode? sendPMode, ReceivePMode? receivePMode, ErrorResult? error) DeterminePModes(
        AS4Message message,
        SendPMode? currentSendingPMode,
        ReceivePMode? currentReceivingPMode)
    {
        var receivingPMode = currentReceivingPMode;
        var sendingPMode = currentSendingPMode;
        var signalMessageMustBeForwarded = false;
        ErrorResult? error = null;

        var firstNonPullRequestSignal =
            message.PrimaryMessageUnit is PullRequest
                ? message.SignalMessages.Skip(1).FirstOrDefault()
                : message.FirstSignalMessage;

        if (firstNonPullRequestSignal is not null)
        {
            var signalHandling = DetermineSignalHandlingInformation(firstNonPullRequestSignal, currentSendingPMode);

            if (!signalHandling.signalMustBeForwarded &&
                signalHandling.sendingPMode == null)
            {
                throw new InvalidOperationException(
                    $"Unable to process received SignalMessage {firstNonPullRequestSignal.MessageId} because no UserMessage was found on this MSH "
                    + $"that is referenced by the received SignalMessage (RefToMessageId {firstNonPullRequestSignal.RefToMessageId})");
            }

            signalMessageMustBeForwarded = signalHandling.signalMustBeForwarded;
            sendingPMode = signalHandling.sendingPMode;
        }

        if (currentReceivingPMode == null && (message.HasUserMessage || signalMessageMustBeForwarded))
        {
            var userMessage = GetUserMessageFromFirstMessageUnitOrRoutingInput(message);

            var result = DetermineReceivingPMode(userMessage);

            receivingPMode = result.pmode;
            error = result.error;
        }

        return (sendingPMode, receivingPMode, error);
    }

    private (bool signalMustBeForwarded, SendPMode? sendingPMode) DetermineSignalHandlingInformation(SignalMessage signal, SendPMode? currentSendingPMode)
    {
        if (currentSendingPMode != null && !signal.IsMultihopSignal)
        {
            // When we're in a sync - push scenario without Multihop, we already know
            // that the signal must not be forwarded and we already know the sending pmode
            // that was used to send the UserMessage, since we still have that state in our MessagingContext.
            return (signalMustBeForwarded: false, sendingPMode: currentSendingPMode);
        }

        if (string.IsNullOrWhiteSpace(signal.RefToMessageId))
        {
            // When we're in the rare event that we receive a (non-pullrequest) signal that has
            // no RefToMessageId, we log this here and assume that it should be forwarded
            // when the signal is a multihop signal. If it is not a multihop signal, then it 
            // should definitely not be forwarded.
            _logger.LogWarning(
                "Cannot determine SendingPMode for received {Signal} SignalMessage "
                + "because it doesn't contain a RefToMessageId to link an UserMessage from which the SendingPMode needs to be selected",
                signal.GetType().Name);

            return (signalMustBeForwarded: signal.IsMultihopSignal, sendingPMode: null);
        }

        // When we get to here, we must inspect our datastore to retrieve the correct state.
        // We try to get the information of the related UserMessage for this signal.
        // If the UserMessage is an intermediary, this signal will have to be forwarded as well.
        // If the UserMessage is not an intermediary, this signal should not be forwarded
        return _repository
            .GetOutMessageData
            (
                where: m => m.EbmsMessageType == MessageType.UserMessage && m.EbmsMessageId == signal.RefToMessageId,
                selection: m => new { m.PMode, m.ModificationTime, m.Intermediary }
            )
            .OrderByDescending(m => m.ModificationTime)
            .FirstOrNothing()
            .Where(x => !string.IsNullOrWhiteSpace(x.PMode))
            .Select(x => (x.Intermediary, AS4XmlSerializer.FromString<SendPMode>(x.PMode)))
            .GetOrElse(() => (signal.IsMultihopSignal, null));
    }

    private UserMessage GetUserMessageFromFirstMessageUnitOrRoutingInput(AS4Message as4Message)
    {
        if (as4Message.HasUserMessage)
        {
            _logger.LogTrace("Primary message unit is a UserMessage; use this UserMessage to determine the ReceivingPMode");
            return as4Message.FirstUserMessage!;
        }

        var routedUserMessage =
            as4Message.SignalMessages.FirstOrDefault(s => s.IsMultihopSignal && !s.IsPullRequest)?.MultiHopRouting;

        if (routedUserMessage is not null)
        {
            _logger.LogDebug("AS4Message is a Multi-Hop SignalMessage; use the embedded routing-information to determine the ReceivingPMode");
            return UserMessageMap.ConvertFromRouting(routedUserMessage.UnsafeGet);
        }

        throw new InvalidOperationException(
            "Incoming message doesn't have a UserMessage either as message unit or as <RoutedInput/> in a SignalMessage. "
            + "This message can therefore not be used to determine the ReceivingPMode");
    }

    private (ReceivePMode? pmode, ErrorResult? error) DetermineReceivingPMode(UserMessage user)
    {
        _logger.LogTrace("Incoming message hasn't yet a ReceivingPMode, will determine one");

        var possibilities = GetMatchingReceivingPModeForUserMessage(user);
        if (!possibilities.Any())
        {
            return (null, NoMatchingPModeFoundFailure());
        }

        if (possibilities.Count() > 1)
        {
            return (null, TooManyPossibilitiesFailure(possibilities));
        }

        var pmode = possibilities.First();
        return (pmode, null);
    }

    private IEnumerable<ReceivePMode> GetMatchingReceivingPModeForUserMessage(UserMessage userMessage)
    {
        var participants = _config.GetReceivingPModes()
            .Select(pmode => new PModeParticipant(pmode, userMessage))
            .Select(_pmodeRuleEngine.ApplyRules);

        var scoresToConsider = participants.Select(p => p.Points).Where(p => p >= 10);
        if (!scoresToConsider.Any())
        {
            return [];
        }

        var maxPoints = scoresToConsider.Max();
        return participants.Where(p => p.Points == maxPoints).Select(p => p.PMode);
    }

    private ErrorResult TooManyPossibilitiesFailure(IEnumerable<ReceivePMode> possibilities)
    {
        var message = "Cannot determine ReceivingPMode because more than a single matching PMode was found (greater or equal than 10 points). "
            + Environment.NewLine + " Please make the matching information more strict in the message packaging information so that only a single PMode is matched."
            + Environment.NewLine + $"{string.Join(Environment.NewLine, possibilities.Select(p => $" - {p.Id}"))}";
        _logger.LogError(message);

        return new ErrorResult(
            "Cannot determine ReceivingPMode because more than a single matching PMode was found",
            ErrorAlias.ProcessingModeMismatch);
    }

    private ErrorResult NoMatchingPModeFoundFailure()
    {
        var message =
            "Cannot determine ReceivingPMode because no configured PMode matched the message packaging information enough (greater or equal than 10 points). "
            + Environment.NewLine + " Please change the message packaging information of your ReceivingPMode(s) to match the message: "
            + Environment.NewLine + " - PMode.Id"
            + Environment.NewLine + " - PMode.MessagePacakging.PartyInfo.FromParty"
            + Environment.NewLine + " - PMode.MessagePacakging.PartyInfo.ToParty"
            + Environment.NewLine + " - PMode.MessagePackaging.CollaborationInfo.Service"
            + Environment.NewLine + " - PMode.MessagePackaging.CollaborationInfo.Action"
            + Environment.NewLine + " See the above trace logging to see for which rules your PMode has accuired points";
        _logger.LogError(message);

        return new ErrorResult(
            "Cannot determine ReceivingPMode because no configured PMode matched the message packaging information",
            ErrorAlias.ProcessingModeMismatch);
    }
}
