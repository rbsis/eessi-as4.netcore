using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Notify;
using Eu.EDelivery.AS4.Strategies.Sender;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Eu.EDelivery.AS4.UnitTests.Strategies.Method;
using Eu.EDelivery.AS4.UnitTests.Strategies.Sender;
using Microsoft.Extensions.Logging;
using Moq;
using NSubstitute;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Notify;

/// <summary>
/// Testing <see cref="SendNotifyMessageStep" />
/// </summary>
public class GivenSendNotifyMessageStepFacts : GivenDatastoreFacts
{
    [Theory]
    [ClassData(typeof(NotifyRetryData))]
    public async Task UpdatesToBeRetriedWhenSendingResultsInRetryableFail<T>(
        NotifyRetry retry,
        NotifyType<T> type) where T : Entity
    {
        // Arrange
        var sut = CreateSendNotifyStepWithSender(StubSenderWithResult(retry.SendResult));

        var ebmsMessageId = Guid.NewGuid().ToString();
        var entity = type.Insertion(GetDataStoreContext)(ebmsMessageId, retry.CurrentRetryCount, retry.MaxRetryCount);

        // Act
        await sut.ExecuteAsync(CreateNotifyMessage<T>(ebmsMessageId, entity), CancellationToken.None);

        // Assert
        type.Assertion(GetDataStoreContext)(
            ebmsMessageId,
            e =>
            {
                Assert.NotNull(e);
                (var _, var actualOperation) = type.OperationGetter(GetDataStoreContext, e);
                Assert.Equal(retry.ExpectedOperation, actualOperation);
            });
    }

    private static INotifySender StubSenderWithResult(SendResult r)
    {
        var stub = new Mock<INotifySender>();
        stub.Setup(s => s.SendAsync(It.IsAny<NotifyMessageEnvelope>(), CancellationToken.None))
            .ReturnsAsync(r);

        return stub.Object;
    }

    private static MessagingContext CreateNotifyMessage<T>(string ebmsMessageId, Entity entity)
    {
        var envelope = new NotifyMessageEnvelope(
            new MessageInfo
            {
                MessageId = ebmsMessageId,
                RefToMessageId = ebmsMessageId
            },
            Status.Delivered,
            [],
            "content-type",
            typeof(T));

        var ctx = new MessagingContext(
            new ReceivedEntityMessage(entity),
            MessagingContextMode.Notify)
        {
            SendingPMode = new SendingProcessingMode
            {
                ReceiptHandling =
                {
                    NotifyMethod = new Method { Type = "FILE" }
                },
                ErrorHandling =
                {
                    NotifyMethod = new Method { Type = "FILE" }
                },
                ExceptionHandling =
                {
                    NotifyMethod = new Method { Type = "FILE" }
                }
            }
        };

        ctx.ModifyContext(envelope);
        return ctx;
    }

    [Fact]
    public async Task ThenExecuteStepFailsWithConnectionFailureAsync()
    {
        // Arrange
        var sut = CreateSendNotifyStepWithSender(new SaboteurSender());

        var fixture = new MessagingContext(
            EmptyNotifyMessageEnvelope(Status.Delivered));

        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(
                () => sut.ExecuteAsync(fixture, CancellationToken.None));
    }

    [Fact]
    public async Task ThenExecuteStepSucceedsWithSendingPModeAsync()
    {
        // Arrange
        var entity = new InMessage($"receipt-{Guid.NewGuid()}");
        entity.InitializeIdFromDatabase(1);

        var fixture = new MessagingContext(
            EmptyNotifyMessageEnvelope(Status.Delivered),
            new ReceivedEntityMessage(entity))
        {
            SendingPMode = new SendingProcessingMode
            {
                ReceiptHandling = { NotifyMethod = new LocationMethod("not-empty-location") }
            }
        };

        GetDataStoreContext.InsertInMessage(new InMessage($"entity-{Guid.NewGuid()}"));

        var spySender = new SpySender();
        var sut = CreateSendNotifyStepWithSender(spySender);

        // Act
        await sut.ExecuteAsync(fixture, CancellationToken.None);

        // Assert
        Assert.True(spySender.IsNotified);
    }

    [Fact]
    public async Task ThenExecuteStepWithReceivingPModeAsync()
    {
        // Arrange
        var entity = new InMessage($"error-{Guid.NewGuid()}");
        entity.InitializeIdFromDatabase(1);

        var fixture = new MessagingContext(
            EmptyNotifyMessageEnvelope(Status.Error),
            new ReceivedEntityMessage(entity))
        {
            SendingPMode = new SendingProcessingMode
            {
                ErrorHandling = { NotifyMethod = new LocationMethod("not-empty-location") }
            }
        };

        GetDataStoreContext.InsertInMessage(new InMessage($"entity-{Guid.NewGuid()}"));

        var spySender = new SpySender();
        var sut = CreateSendNotifyStepWithSender(spySender);

        // Act
        await sut.ExecuteAsync(fixture, CancellationToken.None);

        // Assert
        Assert.True(spySender.IsNotified);
    }

    private IStep CreateSendNotifyStepWithSender(INotifySender sender)
    {
        var stubProvider = new Mock<INotifySenderProvider>();
        stubProvider.Setup(p => p.GetNotifySender(It.IsAny<Method>())).Returns(sender);

        return new SendNotifyMessageStep(
            Substitute.For<ILogger<SendNotifyMessageStep>>(),
            stubProvider.Object,
            Default.NewDatastoreRepository(this),
            Default.NewMarkForRetryService(this));
    }

    private static NotifyMessageEnvelope EmptyNotifyMessageEnvelope(Status status)
    {
        return new NotifyMessageEnvelope(
            messageInfo: new MessageInfo { MessageId = "not-empty-message-id" },
            statusCode: status,
            notifyMessage: [],
            contentType: string.Empty,
            entityType: typeof(InMessage));
    }
}
