using Eu.EDelivery.AS4.Mappings.Core;
using Eu.EDelivery.AS4.Model.Core;
using FsCheck;

namespace Eu.EDelivery.AS4.UnitTests.Mappings.Core;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "<Pending>")]
public class GivenMessageUnitMapFacts
{
    [CustomProperty]
    public Property MappingUserMessageBackAndForthStaysTheSame(UserMessage userMessage)
    {
        // Act
        var xml = UserMessageMap.Convert(userMessage);
        var result = UserMessageMap.Convert(xml);

        // Assert
        return userMessage.CollaborationInfo.Equals(result.CollaborationInfo).Label("equal collaboration")
               .And(userMessage.Sender.Equals(result.Sender).Label("equal sender"))
               .And(userMessage.Receiver.Equals(result.Receiver).Label("equal receiver"))
               .And(userMessage.PayloadInfo.SequenceEqual(result.PayloadInfo).Label("equal part infos"))
               .And(userMessage.MessageProperties.SequenceEqual(result.MessageProperties).Label("equal message properties"));
    }

    [CustomProperty]
    public Property MappingRoutingUserMessageBackAndForthReverseSenderReceiverParty(UserMessage userMessage)
    {
        // Act
        var xml = UserMessageMap.ConvertToRouting(userMessage);
        var result = UserMessageMap.ConvertFromRouting(xml);

        // Assert
        return userMessage.CollaborationInfo.Equals(result.CollaborationInfo).Label("equal collaboration")
               .And(userMessage.Sender.Equals(result.Receiver).Label("equal reversed sender"))
               .And(userMessage.Receiver.Equals(result.Sender).Label("equal reversed receiver"))
               .And(userMessage.PayloadInfo.SequenceEqual(result.PayloadInfo).Label("equal part infos"))
               .And(userMessage.MessageProperties.SequenceEqual(result.MessageProperties).Label("equal message properties"));
    }

    [CustomProperty]
    public Property MappingReceiptBackAndForthStaysTheSame(Receipt receipt)
    {
        // Act
        var result = ReceiptMap.Convert(ReceiptMap.Convert(receipt), receipt.MultiHopRouting);

        // Assert
        return receipt.MessageId.Equals(result.MessageId).Label("equal message id")
            .And(receipt.RefToMessageId?.Equals(result.RefToMessageId).Label("equal ref to message id"))
            .And(receipt.NonRepudiationInformation?.Equals(result.NonRepudiationInformation).Label("equal non repudiation"))
            .And(receipt.MultiHopRouting.Equals(result.MultiHopRouting)).Label("equal routing usermessage");
    }

    [CustomProperty]
    public Property MappingErrorBackAndForthStaysTheSame(Error error)
    {
        // Act
        var result = ErrorMap.Convert(ErrorMap.Convert(error), error.MultiHopRouting);

        // Assert
        return error.MessageId.Equals(result.MessageId).Label("equal message id")
            .And(error.RefToMessageId?.Equals(result.RefToMessageId).Label("equal ref to message id"))
            .And(error.ErrorLines.SequenceEqual(result.ErrorLines).Label("equal error lines"))
            .And(error.MultiHopRouting.Equals(result.MultiHopRouting).Label("equal routing usermessage"));
    }
}
