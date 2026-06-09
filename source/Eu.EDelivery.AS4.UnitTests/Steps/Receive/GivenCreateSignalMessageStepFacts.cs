using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive;
using Eu.EDelivery.AS4.TestUtils.Repositories;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using MessageExchangePattern = Eu.EDelivery.AS4.Entities.MessageExchangePattern;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Receive;

public class GivenCreateSignalMessageStepFacts : GivenDatastoreFacts
{
    private readonly InMemoryMessageBodyStore _messageBodyStore = new(Default.SerializerProvider);

    protected override void Disposing()
    {
        _messageBodyStore.Dispose();
        base.Disposing();
    }

    [CustomProperty]
    public Property ReturnsEmptySoapForSignalsIfReplyPatternIsCallbackOrModeIsPullReceive(
        SignalMessage signal,
        ReplyPattern pattern,
        MessagingContextMode mode)
    {
        // Arrange
        var context =
            new MessagingContext(AS4Message.Create(signal), mode)
            {
                ReceivingPMode = new ReceivingProcessingMode { ReplyHandling = { ReplyPattern = pattern } },
                SendingPMode = new SendingProcessingMode { Id = "sending-pmode" }
            };

        // Act
        var result = ExerciseStoreSignalMessageAsync(context).GetAwaiter().GetResult();

        // Assert
        var actual = result.MessagingContext.AS4Message;
        Assert.NotNull(actual);
        var expected = context.AS4Message;
        Assert.NotNull(expected);

        var isCallback = pattern == ReplyPattern.Callback;
        var isResponse = pattern == ReplyPattern.Response;
        var isPullReceive = mode == MessagingContextMode.PullReceive;
        var isSignal = expected.Equals(actual);

        return (actual.IsEmpty == (isCallback || isPullReceive))
            .Label("Should be an empty SOAP envelope when configured Callback or in PullReceive mode")
            .Or(isSignal == isResponse)
            .Label("Should be a SignalMessage when configured Response")
            .Classify(actual.IsEmpty, "Empty SOAP envelope response")
            .Classify(isSignal, "SignalMessage response");
    }

    [Theory]
    [InlineData(MessageExchangePattern.Pull, ReplyPattern.PiggyBack, Operation.ToBePiggyBacked)]
    [InlineData(MessageExchangePattern.Push, ReplyPattern.Callback, Operation.ToBeSent)]
    [InlineData(MessageExchangePattern.Push, ReplyPattern.PiggyBack, Operation.NotApplicable)]
    [InlineData(MessageExchangePattern.Pull, ReplyPattern.Callback, Operation.ToBeSent)]
    public async Task StoresSignalMessageWithExpectedOperationAccordingToMEPAndReplyPattern(
        MessageExchangePattern mep,
        ReplyPattern reply,
        Operation op)
    {
        // Arrange
        var userMessageId = $"user-{Guid.NewGuid()}";
        GetDataStoreContext.InsertInMessage(
            new InMessage(userMessageId) { MEP = mep });

        var receipt = new Receipt($"receipt-{Guid.NewGuid()}", userMessageId);
        var context = new MessagingContext(
            AS4Message.Create(receipt),
            MessagingContextMode.Receive)
        {
            SendingPMode = new SendingProcessingMode { Id = "shortcut-send-pmode-retrieval" },
            ReceivingPMode = new ReceivingProcessingMode
            {
                ReplyHandling = { ReplyPattern = reply }
            }
        };

        // Act
        await ExerciseStoreSignalMessageAsync(context);

        // Assert
        GetDataStoreContext.AssertOutMessage(
            receipt.MessageId,
            m =>
            {
                Assert.NotNull(m);
                Assert.Equal(op, m.Operation);
            });
    }

    [Property]
    public void StoresRetryInformationForToBePiggyBackedSignalMessages(bool isEnabled, int maxRetryCount, TimeSpan retryInterval)
    {
        // Arrange
        var userMessageId = $"user-{Guid.NewGuid()}";
        GetDataStoreContext.InsertInMessage(
            new InMessage(userMessageId) { MEP = MessageExchangePattern.Pull });

        var receipt = new Receipt($"receipt-{Guid.NewGuid()}", userMessageId);
        var context = new MessagingContext(
            AS4Message.Create(receipt),
            MessagingContextMode.Receive)
        {
            SendingPMode = new SendingProcessingMode { Id = "shortcut-send-pmode-retrieval" },
            ReceivingPMode = new ReceivingProcessingMode
            {
                ReplyHandling =
                {
                    ReplyPattern = ReplyPattern.PiggyBack,
                    PiggyBackReliability = new AS4.Model.PMode.RetryReliability
                    {
                        IsEnabled = isEnabled,
                        RetryCount = maxRetryCount,
                        RetryInterval = retryInterval.ToString()
                    }
                }
            }
        };

        // Act
        ExerciseStoreSignalMessageAsync(context)
            .GetAwaiter()
            .GetResult();

        // Assert
        GetDataStoreContext.AssertOutMessage(
            receipt.MessageId,
            m => GetDataStoreContext.AssertRetryRelatedOutMessage(
                m?.Id ?? 0,
                r =>
                {
                    Assert.True(
                        isEnabled == (0 == r?.CurrentRetryCount),
                        $"Enabling PiggyBack Reliability should result in 0 = {r?.CurrentRetryCount}");
                    Assert.True(
                        isEnabled == (maxRetryCount == r?.MaxRetryCount),
                        $"Enabling PiggyBack Reliability should result in {maxRetryCount} = {r?.MaxRetryCount}");
                    Assert.True(
                        isEnabled == (retryInterval == r?.RetryInterval),
                        $"Enabling PiggyBack Reliability should result in {retryInterval} = {r?.RetryInterval}");
                }));
    }

    [Fact]
    public async Task FailsToStoreSignalMessageWhenReplyPatternResponseForPulledUserMessage()
    {
        // Arrange
        var userMessageId = $"user-{Guid.NewGuid()}";
        GetDataStoreContext.InsertInMessage(
            new InMessage(userMessageId) { MEP = MessageExchangePattern.Pull });

        var receipt = new Receipt($"receipt-{Guid.NewGuid()}", userMessageId);
        var context = new MessagingContext(
            AS4Message.Create(receipt),
            MessagingContextMode.Receive)
        {
            SendingPMode = new SendingProcessingMode { Id = "shortcut-send-pmode-retrieval" },
            ReceivingPMode = new ReceivingProcessingMode
            {
                ReplyHandling = { ReplyPattern = ReplyPattern.Response }
            }
        };

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExerciseStoreSignalMessageAsync(context));
    }

    private async Task<StepResult> ExerciseStoreSignalMessageAsync(MessagingContext ctx)
    {
        var sut = new CreateAS4SignalMessageStep(
            Substitute.For<ILogger<CreateAS4SignalMessageStep>>(),
            Default.NewOutMessageService(this, _messageBodyStore),
            Default.NewPiggyBackingService(this, _messageBodyStore));

        return await sut.ExecuteAsync(ctx, CancellationToken.None);
    }
}

