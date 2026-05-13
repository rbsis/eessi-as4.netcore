using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Transformers;

namespace Eu.EDelivery.AS4.UnitTests.Transformers;

/// <summary>
/// Testing <see cref="NotifyMessageTransformer"/> to notify exceptions
/// </summary>
public class GivenExceptionToNotifyMessageTransformerFacts
{
    [Fact]
    public async Task ThenInExceptionIsTransformedToNotifyEnvelope()
    {
        // Arrange
        var receivedMessage = CreateReceivedExceptionMessage(InException.ForEbmsMessageId("id", "error"), Operation.ToBeNotified);
        var transformer = new NotifyMessageTransformer(Default.IdentifierFactory, Default.AS4MessageTransformer);
        var result = await transformer.TransformAsync(receivedMessage, CancellationToken.None);

        Assert.NotNull(result.NotifyMessage);
        Assert.Equal(
            ((ExceptionEntity)receivedMessage.Entity).EbmsRefToMessageId,
            result.NotifyMessage.MessageInfo.RefToMessageId);
    }

    [Fact]
    public async Task ThenOutExceptionIsTransformedToNotifyEnvelope()
    {
        // Arrange
        var receivedMessage = CreateReceivedExceptionMessage(OutException.ForEbmsMessageId("id", "error"), Operation.ToBeNotified);
        var transformer = new NotifyMessageTransformer(Default.IdentifierFactory, Default.AS4MessageTransformer);

        // Act
        var result = await transformer.TransformAsync(receivedMessage, CancellationToken.None);

        // Assert
        Assert.NotNull(result.NotifyMessage);
        Assert.Equal(
            ((ExceptionEntity)receivedMessage.Entity).EbmsRefToMessageId,
            result.NotifyMessage.MessageInfo.RefToMessageId);
    }

    [Fact]
    public async Task ThenTransformSucceedsWithValidInExceptionForErrorProperties()
    {
        // Arrange            
        var receivedMessage = CreateReceivedExceptionMessage(InException.ForEbmsMessageId("id", "error"), Operation.ToBeNotified);
        var transformer = new NotifyMessageTransformer(Default.IdentifierFactory, Default.AS4MessageTransformer);

        // Act
        var result = await transformer.TransformAsync(receivedMessage, CancellationToken.None);

        // Assert
        Assert.NotNull(result.NotifyMessage);
        Assert.Equal(Status.Exception, result.NotifyMessage.StatusCode);
        Assert.Equal(
            ((InException)receivedMessage.Entity).EbmsRefToMessageId,
            result.NotifyMessage.MessageInfo.RefToMessageId);
    }

    private static ReceivedEntityMessage CreateReceivedExceptionMessage(ExceptionEntity exceptionEntity, Operation exceptionOperation)
    {
        exceptionEntity.Operation = exceptionOperation;

        return new ReceivedEntityMessage(exceptionEntity);
    }

    [Fact]
    public async Task FaisToTransformIfNotSupported()
    {
        // Arrange
        var sut = new NotifyMessageTransformer(Default.IdentifierFactory, Default.AS4MessageTransformer);

        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(
            () => sut.TransformAsync(new ReceivedMessage(Stream.Null), CancellationToken.None));

        await Assert.ThrowsAnyAsync<Exception>(
            () => sut.TransformAsync(new ReceivedEntityMessage(new InMessage(Guid.NewGuid().ToString())), CancellationToken.None));
    }
}
