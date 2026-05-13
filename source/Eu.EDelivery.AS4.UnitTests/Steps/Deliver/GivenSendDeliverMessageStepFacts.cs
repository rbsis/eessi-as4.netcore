using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Deliver;
using Eu.EDelivery.AS4.Strategies.Sender;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Eu.EDelivery.AS4.UnitTests.Strategies.Sender;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RetryReliability = Eu.EDelivery.AS4.Entities.RetryReliability;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Deliver;

/// <summary>
/// Testing <see cref="SendDeliverMessageStep" />
/// </summary>
public class GivenSendDeliverMessageStepFacts : GivenDatastoreFacts
{
    [Fact]
    public async Task ThenExecuteStepFailsWithFailedSenderAsync()
    {
        // Arrange
        var envelope = EmptyDeliverMessageEnvelope();
        var sut = CreateSendDeliverStepWithSender(new SaboteurSender());

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => sut.ExecuteAsync(new MessagingContext(envelope), CancellationToken.None));
    }

    [Fact]
    public async Task ThenExecuteStepSucceedsWithValidSenderAsync()
    {
        // Arrange
        var envelope = EmptyDeliverMessageEnvelope();

        var spySender = new Mock<IDeliverSender>();
        spySender.Setup(s => s.SendAsync(envelope, CancellationToken.None))
                 .ReturnsAsync(SendResult.Success);

        var sut = CreateSendDeliverStepWithSender(spySender.Object);

        // Act
        await sut.ExecuteAsync(new MessagingContext(envelope) { ReceivingPMode = CreateDefaultReceivingPMode() }, CancellationToken.None);

        // Assert
        spySender.Verify(s => s.SendAsync(It.IsAny<DeliverMessageEnvelope>(), CancellationToken.None), Times.Once);
    }

    private IStep CreateSendDeliverStepWithSender(IDeliverSender spySender)
    {
        var stubProvider = new Mock<IDeliverSenderProvider>();
        stubProvider.Setup(p => p.GetDeliverSender(It.IsAny<Method>())).Returns(spySender);

        return new SendDeliverMessageStep(NullLogger<SendDeliverMessageStep>.Instance, stubProvider.Object, Default.NewMarkForRetryService(this));
    }

    private static DeliverMessageEnvelope EmptyDeliverMessageEnvelope()
    {
        return new DeliverMessageEnvelope(
            messageInfo: new MessageInfo("not-empty-message-id", "not-empty-mpc"),
            deliverMessage: [],
            contentType: string.Empty);
    }

    [Fact]
    public async Task ThenExecuteMethodSucceedsWithValidUserMessageAsync()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        GetDataStoreContext.InsertInMessage(
            CreateInMessage(id, InStatus.Received, Operation.ToBeDelivered));

        var envelope = AnonymousDeliverEnvelope(id);
        var sut = CreateSendDeliverStepWithSender(new SpySender());

        // Act
        await sut.ExecuteAsync(new MessagingContext(envelope) { ReceivingPMode = CreateDefaultReceivingPMode() }, CancellationToken.None);

        // Assert
        GetDataStoreContext.AssertInMessage(id, inmessage =>
        {
            Assert.NotNull(inmessage);
            Assert.Equal(InStatus.Delivered, inmessage.Status.ToEnum<InStatus>());
            Assert.Equal(Operation.Delivered, inmessage.Operation);
        });
    }

    [Theory]
    [ClassData(typeof(DeliverRetryData))]
    public async Task ResetInMessageOperationToBeDeliveredWhenCurrentRetryLessThenMaxRetry(DeliverRetry input)
    {
        // Arrange
        var id = Guid.NewGuid().ToString();

        var im = CreateInMessage(id, InStatus.Received, Operation.Delivering);
        GetDataStoreContext.InsertInMessage(im);

        var r = RetryReliability.CreateForInMessage(
            refToInMessageId: im.Id,
            maxRetryCount: input.MaxRetryCount,
            retryInterval: default,
            type: RetryType.Notification);
        r.CurrentRetryCount = input.CurrentRetryCount;
        GetDataStoreContext.InsertRetryReliability(r);

        var envelope = AnonymousDeliverEnvelope(id);

        var stub = new Mock<IDeliverSender>();
        stub.Setup(s => s.SendAsync(envelope, CancellationToken.None))
            .ReturnsAsync(input.SendResult);

        var sut = CreateSendDeliverStepWithSender(stub.Object);

        // Act
        await sut.ExecuteAsync(new MessagingContext(envelope) { ReceivingPMode = CreateDefaultReceivingPMode() }, CancellationToken.None);

        // Assert
        GetDataStoreContext.AssertInMessage(id, inMessage =>
        {
            Assert.NotNull(inMessage);
            Assert.Equal(input.ExpectedStatus, inMessage.Status.ToEnum<InStatus>());
            Assert.Equal(input.ExpectedOperation, inMessage.Operation);
        });
    }

    private static InMessage CreateInMessage(string id, InStatus status, Operation operation)
    {
        var m = new InMessage(id);
        m.SetStatus(status);
        m.Operation = operation;

        return m;
    }

    private static DeliverMessageEnvelope AnonymousDeliverEnvelope(string id) => new(
        messageInfo: new MessageInfo { MessageId = id },
        deliverMessage: [],
        contentType: string.Empty);

    private static ReceivingProcessingMode CreateDefaultReceivingPMode() => new()
    {
        MessageHandling =
        {
            Item = new AS4.Model.PMode.Deliver
            {
                DeliverMethod =
                {
                    Type = "FILE"
                }
            }
        }
    };
}
