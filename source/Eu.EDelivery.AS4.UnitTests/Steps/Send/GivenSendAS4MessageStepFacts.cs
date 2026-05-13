using System.Net;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Http;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps.Send;
using Eu.EDelivery.AS4.Steps.Send.Response;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Extensions;
using Eu.EDelivery.AS4.UnitTests.Model;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using RetryReliability = Eu.EDelivery.AS4.Entities.RetryReliability;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send;

public class GivenSendAS4MessageStepFacts : GivenDatastoreFacts
{
    [Fact]
    public async Task UseReceivingPModeWhenNoSendingPModeIsAvailable()
    {
        // Arrange
        var userMessage = new UserMessage($"user-{Guid.NewGuid()}");
        var message = AS4Message.Create(userMessage);

        var output = new MemoryStream();
        await Default.SerializerProvider
            .Get(message.ContentType)
            .SerializeAsync(message, output, CancellationToken.None);

        var ctx = new MessagingContext(
            message,
            new ReceivedMessage(output, message.ContentType),
            MessagingContextMode.Send)
        {
            ReceivingPMode = new ReceivingProcessingMode
            {
                ReplyHandling =
                {
                    ResponseConfiguration = new PushConfiguration
                    {
                        Protocol = { Url = "http://some/endpoint/path" }
                    }
                }
            }
        };

        var receipt = new Receipt($"receipt-{Guid.NewGuid()}", userMessage.MessageId);
        var stub = StubHttpClient.ThatReturns(AS4Message.Create(receipt));

        var sut = CreateSendStepWithResponse(stub);

        // Act
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded, "Sending UserMessage step has not succeeded");
        Assert.NotNull(result.MessagingContext.AS4Message);
        Assert.Equal(receipt, result.MessagingContext.AS4Message.PrimaryMessageUnit);

        // TearDown
        ctx.Dispose();
    }

    [Fact]
    public async Task UpdateRetryReliabilityToPendingWhenReceiverIsOffline()
    {
        // Arrange
        var ebmsMessageId = $"user-{Guid.NewGuid()}";
        var tobeSendMessage = AS4Message.Create(new UserMessage(ebmsMessageId));

        var outMessage = new OutMessage(ebmsMessageId);
        GetDataStoreContext.InsertOutMessage(outMessage);
        GetDataStoreContext.InsertRetryReliability(
            RetryReliability.CreateForOutMessage(
                refToOutMessageId: outMessage.Id,
                maxRetryCount: 2,
                retryInterval: TimeSpan.FromSeconds(1),
                type: RetryType.Send));

        var ctx = new MessagingContext(
            tobeSendMessage,
            new ReceivedEntityMessage(
                outMessage,
                tobeSendMessage.ToStream(),
                tobeSendMessage.ContentType),
            MessagingContextMode.Send)
        {
            SendingPMode = CreateSendPModeWithPushUrl()
        };

        var sabotageException = new WebException("Remote host not available");
        var sut = CreateSendStepWithResponse(
            StubHttpClient.ThatThrows(sabotageException));

        // Act / Assert
        var actualException = await Assert.ThrowsAsync<WebException>(
            () => sut.ExecuteAsync(ctx, CancellationToken.None));

        Assert.Equal(sabotageException, actualException);

        GetDataStoreContext.AssertRetryRelatedOutMessage(
            outMessage.Id,
            r =>
            {
                Assert.NotNull(r);
                Assert.Equal(RetryStatus.Pending, r.Status);
            });
    }

    [Fact]
    public async Task AfterSendUpdatesRequestOperationAndStatusToSentForExsitingSendPMode()
    {
        // Arrange
        var ebmsMessageId = $"user-{Guid.NewGuid()}";
        var tobeSentMsg = AS4Message.Create(new FilledUserMessage(ebmsMessageId));

        var inserted = new OutMessage(ebmsMessageId: ebmsMessageId);
        GetDataStoreContext.InsertOutMessage(inserted);

        var receivedMessage = new ReceivedEntityMessage(
            inserted,
            tobeSentMsg.ToStream(),
            tobeSentMsg.ContentType);

        var ctx = new MessagingContext(
            tobeSentMsg,
            receivedMessage,
            MessagingContextMode.Send)
        {
            SendingPMode = CreateSendPModeWithPushUrl()
        };

        var receiptMessage =
            AS4Message.Create(new Receipt($"receipt-{Guid.NewGuid()}", $"user-{Guid.NewGuid()}"));

        // Act 
        var sut = CreateSendStepWithResponse(
            StubHttpClient.ThatReturns(receiptMessage));

        await sut.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        GetDataStoreContext.AssertOutMessage(
            ebmsMessageId,
            message =>
            {
                Assert.NotNull(message);
                Assert.Equal(OutStatus.Sent, message.Status.ToEnum<OutStatus>());
                Assert.Equal(Operation.Sent, message.Operation);
            });
    }

    [Fact]
    public async Task SendResultsInStopExecutionIfResponseIsPullRequestWarningForExsistingSendPMode()
    {
        // Arrange
        var as4Message = AS4Message.Create(Error.CreatePullRequestWarning($"error-{Guid.NewGuid()}"));
        var sut = CreateSendStepWithResponse(
            StubHttpClient.ThatReturns(as4Message));

        var ctx = CreateMessagingContextWithDefaultPullRequest();
        ctx.SendingPMode = CreateSendPModeWithPushUrl();

        // Act
        var actualResult = await sut.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.False(actualResult.CanProceed);
    }

    [Fact]
    public async Task SendReturnsEmptyResponseForEmptyRequestForExistingSendPMode()
    {
        // Arrange
        var sut = CreateSendStepWithResponse(
            StubHttpClient.ThatReturns(AS4Message.Empty, HttpStatusCode.Accepted));

        var ctx = CreateMessagingContextWithDefaultPullRequest();
        ctx.SendingPMode = CreateSendPModeWithPushUrl();

        // Act
        var actualResult = await sut.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.NotNull(actualResult.MessagingContext.AS4Message);
        Assert.True(actualResult.MessagingContext.AS4Message.IsEmpty);
        Assert.False(actualResult.CanProceed);
    }

    private static MessagingContext CreateMessagingContextWithDefaultPullRequest()
    {
        var pullRequest = AS4Message.Create(
            new PullRequest(messageId: "message-id", mpc: null));

        return new MessagingContext(
            new ReceivedMessage(
                pullRequest.ToStream(),
                pullRequest.ContentType),
            MessagingContextMode.Receive);
    }

    private SendAS4MessageStep CreateSendStepWithResponse(IReliableHttpClient client) => new(
        NullLogger<SendAS4MessageStep>.Instance,
        client: client,
        Default.CertificateRepository,
        Default.NewMarkForRetryService(this),
        StubPiggyBackingService.Instance,
        CreatePullRequestResponseHandler(),
        Default.SerializerProvider);

    private static PullRequestResponseHandler CreatePullRequestResponseHandler() => new(
        new EmptyBodyResponseHandler(
            NullLogger<EmptyBodyResponseHandler>.Instance,
            new TailResponseHandler()),
        StubPiggyBackingService.Instance);

    private static SendingProcessingMode CreateSendPModeWithPushUrl()
    {
        return new SendingProcessingMode
        {
            PushConfiguration = new PushConfiguration
            {
                Protocol = { Url = "http://ignored/path" }
            },
            Reliability =
            {
                ReceptionAwareness = { IsEnabled = true }
            }
        };
    }
}
