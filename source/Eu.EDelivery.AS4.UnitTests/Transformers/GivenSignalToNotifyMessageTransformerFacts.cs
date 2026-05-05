using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Transformers;

namespace Eu.EDelivery.AS4.UnitTests.Transformers;

/// <summary>
/// Tests the <see cref="NotifyMessageTransformer"/> to notify on signal-messages
/// </summary>
public class GivenSignalToNotifyMessageTransformerFacts
{
    [Fact]
    public async Task FailsToTransformIfMessageTypeIsNotSupported()
    {
        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(() => ExerciseTransform(new ReceivedMessage(Stream.Null)));
    }

    [Fact]
    public async Task FailsToTransformIfMessageDoesntHaveAnyMatchingSignalMessages()
    {
        // Arrange
        var receival = await CreateInvalidReceivedReceiptMessage();

        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(() => ExerciseTransform(receival));
    }

    [Fact]
    public async Task ThenNotifyMessageHasCorrectStatusCode()
    {
        // Arrange
        var receivedSignal = await CreateReceivedReceiptMessage();

        // Act
        var result = await ExerciseTransform(receivedSignal);

        // Assert
        var notifyMessage = result.NotifyMessage;
        Assert.NotNull(notifyMessage);
        Assert.Equal(Status.Delivered, notifyMessage.StatusCode);
    }

    [Fact]
    public async Task ThenSignalMessageIsTransformedToNotifyEnvelopeWithCorrectContents()
    {
        // Arrange
        var receivedSignal = await CreateReceivedReceiptMessage();

        // Act
        var result = await ExerciseTransform(receivedSignal);

        // Assert
        Assert.NotNull(result.NotifyMessage);

        var notifyMessage =
            await AS4XmlSerializer.FromStringAsync<NotifyMessage>(Encoding.UTF8.GetString(result.NotifyMessage.NotifyMessage), CancellationToken.None);

        Assert.NotNull(notifyMessage);

        // Assert: check if the original Receipt is a part of the NotifyMessage.
        var document = new XmlDocument { PreserveWhitespace = true };
        document.LoadXml(Encoding.UTF8.GetString(((MemoryStream)receivedSignal.UnderlyingStream).ToArray()));

        Assert.Equal(
            Canonicalize(document.SelectSingleNode("//*[local-name()='SignalMessage']")),
            Canonicalize(notifyMessage.StatusInfo.Any.First()));
    }

    [Fact]
    public async Task ThenSignalMessageIsTransformedToNotifyEnvelopeWithCorrectMessageInfo()
    {
        // Arrange
        var receivedSignal = await CreateReceivedReceiptMessage();
        var receivedMessageEntity = (MessageEntity)receivedSignal.Entity;

        // Act
        var result = await ExerciseTransform(receivedSignal);

        // Assert
        Assert.NotNull(result.NotifyMessage);
        Assert.Equal(receivedMessageEntity.EbmsMessageId, result.NotifyMessage.MessageInfo.MessageId);
        Assert.Equal(receivedMessageEntity.EbmsRefToMessageId, result.NotifyMessage.MessageInfo.RefToMessageId);
    }

    private static async Task<MessagingContext> ExerciseTransform(ReceivedMessage receival)
    {
        var sut = new NotifyMessageTransformer(Default.IdentifierFactory, Default.AS4MessageTransformer);

        return await sut.TransformAsync(receival, CancellationToken.None);
    }

    private static string Canonicalize(XmlNode? input)
    {
        Assert.NotNull(input);

        var doc = new XmlDocument();
        doc.LoadXml(input.OuterXml);

        var t = new XmlDsigC14NTransform();
        t.LoadInput(doc);

        var stream = (Stream)t.GetOutput(typeof(Stream));

        return new StreamReader(stream).ReadToEnd();
    }

    private static async Task<ReceivedEntityMessage> CreateInvalidReceivedReceiptMessage()
    {
        var receiptContent = new MemoryStream(Encoding.UTF8.GetBytes(Properties.Resources.receipt));

        var serializer = Default.SerializerProvider.Get(Constants.ContentTypes.Soap);
        var receipt = await serializer.DeserializeAsync(receiptContent, Constants.ContentTypes.Soap, CancellationToken.None);

        receiptContent.Position = 0;
        var receiptInMessage = new InMessage("non-existing-id")
        {
            EbmsMessageType = MessageType.Receipt
        };

        var receivedMessage = new ReceivedEntityMessage(receiptInMessage, receiptContent, receipt.ContentType);

        return receivedMessage;
    }

    private static async Task<ReceivedEntityMessage> CreateReceivedReceiptMessage()
    {
        var receiptContent = new MemoryStream(Encoding.UTF8.GetBytes(Properties.Resources.receipt));

        var serializer = Default.SerializerProvider.Get(Constants.ContentTypes.Soap);
        var receiptMessage = await serializer.DeserializeAsync(receiptContent, Constants.ContentTypes.Soap, CancellationToken.None);

        receiptContent.Position = 0;
        var receiptInMessage = CreateInMessageFor(receiptMessage);

        var receivedMessage = new ReceivedEntityMessage(receiptInMessage, receiptContent, receiptInMessage.ContentType!);

        return receivedMessage;
    }

    private static InMessage CreateInMessageFor(AS4Message receiptMessage)
    {
        Assert.NotNull(receiptMessage.FirstSignalMessage);
        var inMessage = new InMessage(receiptMessage.FirstSignalMessage.MessageId)
        {
            ContentType = Constants.ContentTypes.Soap,
            EbmsRefToMessageId = receiptMessage.FirstSignalMessage.RefToMessageId
        };

        inMessage.SetStatus(InStatus.Received);
        inMessage.Operation = Operation.ToBeNotified;
        inMessage.EbmsMessageType = MessageType.Receipt;

        return inMessage;
    }
}
