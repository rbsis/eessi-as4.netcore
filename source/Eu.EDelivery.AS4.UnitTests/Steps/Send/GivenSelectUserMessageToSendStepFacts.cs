using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Send;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using MessageExchangePattern = Eu.EDelivery.AS4.Entities.MessageExchangePattern;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send;

/// <summary>
/// Testing <see cref="SelectUserMessageToSendStep"/>
/// </summary>
public class GivenSelectUserMessageToSendStepFacts : GivenDatastoreFacts
{
    [Fact]
    public async Task SelectionReturnsPullRequestWarningIfNoMatchesAreFound()
    {
        // Act
        var result = await ExerciseSelection(expectedMpc: null);

        // Assert
        Assert.NotNull(result.MessagingContext.AS4Message);
        var signal = Assert.IsType<Error>(result.MessagingContext.AS4Message.FirstSignalMessage);
        Assert.True(signal.IsPullRequestWarning, "error signal is not a warning for a pull request");
        Assert.False(result.CanProceed);
    }

    [Fact]
    public async Task SelectsUserMessageIfUserMessageMatchesCriteria()
    {
        // Arrange
        const string ExpectedMpc = "message-mpc";
        InsertUserMessage(ExpectedMpc, MessageExchangePattern.Push, Operation.ToBeSent);
        InsertUserMessage("yet-another-mpc", MessageExchangePattern.Pull, Operation.DeadLettered);
        InsertUserMessage(ExpectedMpc, MessageExchangePattern.Pull, Operation.ToBeSent);

        // Act
        var result = await ExerciseSelection(ExpectedMpc);

        // Assert

        var as4Message = await RetrieveAS4MessageFromContext(result.MessagingContext);

        var userMessage = as4Message.FirstUserMessage;

        Assert.NotNull(userMessage);
        Assert.Equal(ExpectedMpc, userMessage.Mpc);
        AssertOutMessage(userMessage.MessageId, m => Assert.True(m.Operation == Operation.Sent));
        Assert.NotNull(result.MessagingContext.SendingPMode);
    }

    private static async Task<AS4Message> RetrieveAS4MessageFromContext(MessagingContext context)
    {
        if (context.ReceivedMessage != null)
        {
            context.ReceivedMessage.UnderlyingStream.Position = 0;

            return await Default.SerializerProvider
                .Get(context.ReceivedMessage.ContentType)
                .DeserializeAsync(
                    context.ReceivedMessage.UnderlyingStream,
                    context.ReceivedMessage.ContentType,
                    CancellationToken.None);
        }

        if (context.AS4Message != null)
        {
            return context.AS4Message;
        }

        throw new InvalidOperationException("A ReceivedMessage was expected in the MessagingContext.");
    }

    private async Task<StepResult> ExerciseSelection(string? expectedMpc)
    {
        var sut = new SelectUserMessageToSendStep(
            Default.NewDatastoreRepository(this),
            Default.InMemoryMessageBodyStore,
            Default.IdentifierFactory);
        var context = ContextWithPullRequest(expectedMpc);

        // Act
        return await sut.ExecuteAsync(context, CancellationToken.None);
    }

    private void InsertUserMessage(string mpc, MessageExchangePattern pattern, Operation operation)
    {
        var sendingPMode = new SendingProcessingMode()
        {
            Id = "SomePModeId",
            MessagePackaging = { Mpc = mpc }
        };

        var userMessage = Default.SendingPModeMap.CreateUserMessage(sendingPMode);
        var as4Message = AS4Message.Create(userMessage, sendingPMode);

        var om = new OutMessage(userMessage.MessageId)
        {
            MEP = pattern,
            Mpc = mpc,
            ContentType = as4Message.ContentType,
            EbmsMessageType = MessageType.UserMessage,
            Operation = operation,
            MessageLocation = Default.InMemoryMessageBodyStore.SaveAS4Message(location: "some-location", message: as4Message)
        };

        om.SetPModeInformation(sendingPMode);
        GetDataStoreContext.InsertOutMessage(om);
    }

    private static MessagingContext ContextWithPullRequest(string? mpc)
    {
        var pullRequest = new PullRequest("message-id", mpc);
        return new MessagingContext(AS4Message.Create(pullRequest), MessagingContextMode.Send);
    }

    private void AssertOutMessage(string messageId, Action<OutMessage> assertion)
    {
        using var context = GetDataStoreContext();
        var outMessage = context.OutMessages.First(m => m.EbmsMessageId.Equals(messageId));
        assertion(outMessage);
    }
}
