using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using MessageExchangePattern = Eu.EDelivery.AS4.Entities.MessageExchangePattern;

namespace Eu.EDelivery.AS4.Builders.Entities;

/// <summary>
/// Builder to create <see cref="OutMessage"/> Models
/// </summary>
internal class OutMessageBuilder
{
    private readonly MessageUnit _messageUnit;
    private readonly string _contentType;
    private readonly IPMode? _pmode;

    private OutMessageBuilder(MessageUnit messageUnit, string contentType, IPMode? pmode)
    {
        _messageUnit = messageUnit;
        _contentType = contentType;
        _pmode = pmode;
    }

    /// <summary>
    /// For a given <paramref name="messageUnit"/>.
    /// </summary>
    /// <param name="messageUnit">The message unit.</param>
    /// <param name="contentType"></param>
    /// <param name="pmode">The PMode that is used for this message</param>
    /// <returns></returns>
    public static OutMessageBuilder ForMessageUnit(MessageUnit? messageUnit, string contentType, IPMode? pmode)
    {
        ArgumentNullException.ThrowIfNull(messageUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        return new OutMessageBuilder(messageUnit, contentType, pmode);
    }

    /// <summary>
    /// Prepare an <see cref="OutMessage"/> to be picked or stored by a Sending operation.
    /// </summary>
    /// <param name="location"></param>
    /// <param name="url"></param>
    /// <param name="status"></param>
    /// <param name="operation"></param>
    /// <returns></returns>
    public OutMessage BuildForSending(
        string location,
        string? url,
        OutStatus status,
        Operation operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        var outMessage = Build();

        outMessage.Url = url;
        outMessage.MessageLocation = location;
        outMessage.SetStatus(status);
        outMessage.Operation = operation;

        return outMessage;
    }

    /// <summary>
    /// Prepare an <see cref="OutMessage"/> to be picked up by the Forward Agent.
    /// </summary>
    /// <param name="location"></param>
    /// <param name="receivedInMessage"></param>
    /// <returns></returns>
    public OutMessage BuildForForwarding(string location, InMessage receivedInMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        ArgumentNullException.ThrowIfNull(receivedInMessage);

        var outMessage = Build();
        outMessage.MessageLocation = location;
        outMessage.Intermediary = true;
        outMessage.IsDuplicate = receivedInMessage.IsDuplicate;
        outMessage.Mpc = (_pmode as SendingProcessingMode)?.MessagePackaging?.Mpc;
        outMessage.Operation = Operation.ToBeProcessed;

        return outMessage;
    }

    private OutMessage Build()
    {
        var outMessage = new OutMessage(_messageUnit.MessageId)
        {
            ContentType = _contentType,
            ModificationTime = DateTimeOffset.Now,
            InsertionTime = DateTimeOffset.Now,
            Operation = Operation.NotApplicable,
            MEP = DetermineMepOf(_pmode as SendingProcessingMode),
            EbmsMessageType = DetermineSignalMessageType(_messageUnit)
        };

        outMessage.SetPModeInformation(_pmode);
        outMessage.AssignAS4Properties(_messageUnit);

        if (!string.IsNullOrWhiteSpace(_messageUnit.RefToMessageId))
        {
            outMessage.EbmsRefToMessageId = _messageUnit.RefToMessageId;
        }

        return outMessage;
    }

    private static MessageType DetermineSignalMessageType(MessageUnit messageUnit) => messageUnit switch
    {
        UserMessage _ => MessageType.UserMessage,
        Receipt _ => MessageType.Receipt,
        Error _ => MessageType.Error,
        _ => throw new NotSupportedException($"There exists no MessageType mapping for the specified MessageUnit type {typeof(MessageUnit)}"),
    };

    private static MessageExchangePattern DetermineMepOf(SendingProcessingMode? pmode) => (pmode?.MepBinding) switch
    {
        MessageExchangePatternBinding.Pull => MessageExchangePattern.Pull,
        _ => MessageExchangePattern.Push,
    };
}
