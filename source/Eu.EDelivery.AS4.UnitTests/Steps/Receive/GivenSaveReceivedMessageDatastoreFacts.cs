using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using MessageExchangePattern = Eu.EDelivery.AS4.Entities.MessageExchangePattern;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Receive;

public class GivenSaveReceivedMessageDatastoreFacts : GivenDatastoreStepFacts
{
    private readonly InMemoryMessageBodyStore _messageBodyStore = new(Default.SerializerProvider);

    public GivenSaveReceivedMessageDatastoreFacts()
    {
        Step = new SaveReceivedMessageStep(
            Substitute.For<ILogger<SaveReceivedMessageStep>>(),
            Default.NewInMessageService(this, _messageBodyStore));
    }

    protected override void Disposing()
    {
        _messageBodyStore.Dispose();
        base.Disposing();
    }

    /// <summary>
    /// Gets a <see cref="IStep" /> implementation to exercise the datastore.
    /// </summary>
    protected override IStep Step { get; }

    [Property(MaxTest = 20)]
    public Property SavesBundledMessageUnitsAsInMessages(MessagingContextMode mode)
    {
        return Prop.ForAll(
            GenMessageUnits().ToArbitrary(),
            messageUnits =>
            {
                // Arrange
                var fixture = AS4Message.Create(messageUnits);
                var stub = new ReceivedMessage(Stream.Null, Constants.ContentTypes.Soap);
                var ctx = new MessagingContext(fixture, stub, mode);

                // Act
                Step.ExecuteAsync(ctx, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                var inserts =
                    GetDataStoreContext.GetInMessages(m => fixture.MessageIds.Contains(m.EbmsMessageId));

                IEnumerable<string> expected = fixture.MessageIds.OrderBy(x => x);
                IEnumerable<string> actual = inserts.Select(i => i.EbmsMessageId).OrderBy(x => x);
                Assert.True(
                    expected.SequenceEqual(actual),
                    $"{string.Join(", ", expected)} != {string.Join(", ", actual)}");

                Assert.All(
                    inserts, m =>
                    {
                        var pushForNonPullReceive = m.MEP == MessageExchangePattern.Push && mode != MessagingContextMode.PullReceive;
                        var pullForPullReceive = m.MEP == MessageExchangePattern.Pull && mode == MessagingContextMode.PullReceive;

                        Assert.True(
                            pushForNonPullReceive || pullForPullReceive,
                            mode == MessagingContextMode.PullReceive
                                ? "MEP Binding should be Pull"
                                : "MEP Binding should be Push");
                    });
            });
    }

    private static Gen<List<MessageUnit>> GenMessageUnits()
    {
        return Gen.OneOf(
            Gen.Fresh<MessageUnit>(() => new Receipt($"receipt-{Guid.NewGuid()}", $"user-{Guid.NewGuid()}")),
            Gen.Fresh<MessageUnit>(() => new UserMessage($"user-{Guid.NewGuid()}")))
                  .NonEmptyListOf();
    }

    [Fact]
    public async Task ThenExecuteStepSucceedsAsync()
    {
        // Arrange
        using var context = CreateReceivedMessagingContext(AS4Message.Empty, receivingPMode: null);

        // Act
        var result = await Step.ExecuteAsync(context, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UserMessageGetsSavedAsDuplicateWhenInMessageExistsWithSameEbmsMessageId()
    {
        // Arrange
        var ebmsMessageId = $"user-{Guid.NewGuid()}";
        GetDataStoreContext.InsertInMessage(new InMessage(ebmsMessageId));

        var user = new UserMessage(ebmsMessageId);
        var context = new MessagingContext(
            AS4Message.Create(user),
            new ReceivedMessage(Stream.Null),
            MessagingContextMode.Receive);

        // Act
        await Step.ExecuteAsync(context, CancellationToken.None);

        // Assert
        var actual = GetDataStoreContext.GetInMessage(
            m => m.EbmsMessageId == ebmsMessageId
                 && m.IsDuplicate);

        Assert.True(actual != null, "Saved UserMessage should be marked as duplicate");
    }

    [Fact]
    public async Task SignalMessageGetsSavedAsDuplicateWhenInMessageExistsWithSameEbmsRefToMessageId()
    {
        // Arrange
        var ebmsMessageId = $"receipt-{Guid.NewGuid()}";
        var ebmsRefToMessageId = $"user-{Guid.NewGuid()}";
        GetDataStoreContext.InsertInMessage(
            new InMessage(ebmsMessageId)
            {
                EbmsRefToMessageId = ebmsRefToMessageId
            });

        var receipt = new Receipt(ebmsMessageId, ebmsRefToMessageId);
        var context = new MessagingContext(
            AS4Message.Create(receipt),
            new ReceivedMessage(Stream.Null),
            MessagingContextMode.Receive);

        // Act
        await Step.ExecuteAsync(context, CancellationToken.None);

        // Assert
        var actual = GetDataStoreContext.GetInMessage(
            m => m.EbmsMessageId == ebmsMessageId
                 && m.EbmsRefToMessageId == ebmsRefToMessageId
                 && m.IsDuplicate);

        Assert.True(actual != null, "Saved Receipt should be marked as duplicate");
    }

    [Fact]
    public async Task DuringSavingTheDeserializedAS4MessageIsUsedInsteadOfDeserializingIncomingStream()
    {
        // Arrange
        var receipt = new Receipt($"receipt-{Guid.NewGuid()}", $"reftoid-{Guid.NewGuid()}");
        var ctx = new MessagingContext(
            AS4Message.Create(receipt),
            new ReceivedMessage(Stream.Null, Constants.ContentTypes.Soap),
            MessagingContextMode.Receive);

        // Act
        await Step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        GetDataStoreContext.AssertInMessage(receipt.MessageId, Assert.NotNull);
    }

    [Fact]
    public async Task ThenExecuteStepIgnoresPullRequests()
    {
        // Arrange
        var pr = AS4Message.Create(
            new PullRequest(
                $"pr-msg-id-{Guid.NewGuid()}",
                $"pr-mpc-{Guid.NewGuid()}"));

        // Act
        using (var ctx =
            CreateReceivedMessagingContext(pr, new ReceivingProcessingMode()))
        {
            await Step.ExecuteAsync(ctx, CancellationToken.None);
        }

        // Assert
        var im = GetDataStoreContext.GetInMessage(
            m => m.EbmsMessageId == pr.GetPrimaryMessageId());
        Assert.Null(im);
    }

    [Fact]
    public async Task ThenExecuteStepSavesBothUserAndReceiptMessage()
    {
        // Arrange
        var um = CreateUserMessage();
        SignalMessage r = CreateReceipt();
        var as4 = AS4Message.Empty;
        as4.AddMessageUnit(um);
        as4.AddMessageUnit(r);

        // Act
        using (var ctx =
            CreateReceivedMessagingContext(as4, new ReceivingProcessingMode()))
        {
            // Act
            await Step.ExecuteAsync(ctx, CancellationToken.None);
        }

        // Assert
        GetDataStoreContext.AssertInMessage(um.MessageId, Assert.NotNull);
        GetDataStoreContext.AssertInMessage(r.MessageId, Assert.NotNull);
    }

    [Fact]
    public async Task ThenExecuteStepIsTestUserMessage()
    {
        // Arrange
        var userMessage = CreateUserMessage();
        var as4Message = AS4Message.Create(userMessage);

        var pmode = new ReceivingProcessingMode();
        pmode.Reliability.DuplicateElimination.IsEnabled = true;

        using var messagingContext = CreateReceivedMessagingContext(as4Message, pmode);
        // Act
        await Step.ExecuteAsync(messagingContext, CancellationToken.None);

        // Assert
        var m = GetUserInMessageForEbmsMessageId(userMessage);
        Assert.Equal(Operation.NotApplicable, m.Operation);
    }

    [Fact]
    public async Task ThenExecuteStepUpdatesDuplicateReceiptMessage()
    {
        // Arrange
        SignalMessage signalMessage = new Receipt($"receipt-{Guid.NewGuid()}", "ref-to-message-id")
        {
            IsDuplicate = false
        };

        using (var messagingContext =
            CreateReceivedMessagingContext(AS4Message.Create(signalMessage), null))
        {
            // Act           
            // Execute the step twice.     
            var stepResult = await Step.ExecuteAsync(messagingContext, CancellationToken.None);
            Assert.NotNull(stepResult.MessagingContext.AS4Message);
            Assert.NotNull(stepResult.MessagingContext.AS4Message.FirstSignalMessage);
            Assert.False(stepResult.MessagingContext.AS4Message.FirstSignalMessage.IsDuplicate);
        }

        using (var messagingContext =
            CreateReceivedMessagingContext(AS4Message.Create(signalMessage), null))
        {
            var stepResult = await Step.ExecuteAsync(messagingContext, CancellationToken.None);

            // Assert
            Assert.NotNull(stepResult.MessagingContext.AS4Message);
            Assert.NotNull(stepResult.MessagingContext.AS4Message.FirstSignalMessage);
            Assert.True(stepResult.MessagingContext.AS4Message.FirstSignalMessage.IsDuplicate);
        }
    }

    [Fact]
    public async Task ThenExecuteStepUpdatesDuplicateUserMessage()
    {
        // Arrange
        var userMessage = CreateUserMessage();
        InsertDuplicateUserMessage(userMessage);

        var pmode = new ReceivingProcessingMode();
        pmode.Reliability.DuplicateElimination.IsEnabled = true;

        using (var context =
            CreateReceivedMessagingContext(AS4Message.Create(userMessage), pmode))
        {
            // Act
            await Step.ExecuteAsync(context, CancellationToken.None);
        }

        // Assert
        var m = GetUserInMessageForEbmsMessageId(userMessage);
        Assert.Equal(Operation.NotApplicable, m.Operation);
    }

    private void InsertDuplicateUserMessage(MessageUnit userMessage)
    {
        GetDataStoreContext.InsertInMessage(new InMessage(ebmsMessageId: userMessage.MessageId));
    }

    private InMessage GetUserInMessageForEbmsMessageId(MessageUnit userMessage)
    {
        var inMessage = GetDataStoreContext
            .GetInMessage(m => m.EbmsMessageId.Equals(userMessage.MessageId));

        Assert.NotNull(inMessage);
        Assert.Equal(MessageType.UserMessage, inMessage.EbmsMessageType);

        return inMessage;
    }

    private static UserMessage CreateUserMessage()
    {
        var userMessageId = Guid.NewGuid().ToString();
        return new UserMessage(userMessageId);
    }

    protected static MessagingContext CreateReceivedMessagingContext(AS4Message as4Message, ReceivingProcessingMode? receivingPMode)
    {
        var stream = new MemoryStream();

        Default.SerializerProvider
            .Get(as4Message.ContentType)
            .Serialize(as4Message, stream);

        stream.Position = 0;

        var ctx = new MessagingContext(
                new ReceivedMessage(stream, as4Message.ContentType),
                MessagingContextMode.Receive)
        { ReceivingPMode = receivingPMode };

        ctx.ModifyContext(as4Message);
        return ctx;
    }
}
