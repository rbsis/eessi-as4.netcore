using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Submit;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PartyInfo = Eu.EDelivery.AS4.Model.Common.PartyInfo;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send;

/// <summary>
/// Testing <see cref="CreateAS4MessageStep" />
/// </summary>
public class GivenCreateAS4MessageStepFacts
{
    public class GivenValidArguments : GivenCreateAS4MessageStepFacts
    {
        [Fact]
        public async Task NoPayloadsToRetrieve()
        {
            // Arrange
            var submit = SubmitWithTwoPayloads();
            submit.PMode = DefaultSendPMode();
            submit.Payloads = [];

            var context = new MessagingContext(submit);

            // Act
            var result = await ExerciseCreateAS4Message(context);

            // Assert
            var actual = result.MessagingContext.AS4Message;
            Assert.NotNull(actual);
            Assert.False(actual.HasAttachments);
        }

        [Fact]
        public async Task AssignsAttachmentLocations()
        {
            // Arrange
            var message = SubmitWithTwoPayloads();
            message.PMode = DefaultSendPMode();

            var context = new MessagingContext(message);

            // Act
            var result = await ExerciseCreateAS4Message(context);

            // Assert
            var actual = result.MessagingContext.AS4Message;
            Assert.NotNull(actual);
            Assert.True(actual.HasAttachments);
            Assert.Equal(Stream.Null, actual.Attachments.First().Content);
        }

        [Fact]
        public async Task ThenStepCreatesAS4Message()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.PMode = DefaultSendPMode();
            var context = new MessagingContext(submitMessage);

            // Act
            var result = await ExerciseCreateAS4Message(context);

            // Assert
            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
        }

        [Fact]
        public async Task ThenStepCreatesAS4MessageWithMessageInfo()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.PMode = DefaultSendPMode();
            var internalMessage = new MessagingContext(submitMessage);

            // Act
            var result = await ExerciseCreateAS4Message(internalMessage);

            // Assert
            var submitMessageInfo = submitMessage.MessageInfo;
            Assert.NotNull(result.MessagingContext.AS4Message);
            var userMessage = result.MessagingContext.AS4Message.FirstUserMessage;
            Assert.NotNull(userMessage);
            Assert.Equal(submitMessageInfo.MessageId, userMessage.MessageId);
            Assert.Equal(Constants.Namespaces.EbmsDefaultMpc, userMessage.Mpc);
            Assert.Equal(submitMessageInfo.RefToMessageId, userMessage.RefToMessageId);
        }

        [Fact]
        public async Task ThenStepCreatesAS4MessageWithMpcFromSubmitMessage()
        {
            var submitMessage = CreateSubmitMessageWithMpc("some-mpc");
            submitMessage.PMode = DefaultSendPMode();
            submitMessage.Collaboration.AgreementRef = new() { PModeId = submitMessage.PMode.Id };
            submitMessage.PMode.AllowOverride = true;
            var context = new MessagingContext(submitMessage);

            var result = await ExerciseCreateAS4Message(context);

            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
            Assert.Equal(result.MessagingContext.AS4Message.FirstUserMessage.Mpc, submitMessage.MessageInfo.Mpc);
        }

        private static SubmitMessage CreateSubmitMessageWithMpc(string mpc)
        {
            var message = new SubmitMessage();

            message.MessageInfo.Mpc = mpc;

            return message;
        }

        [Fact]
        public async Task ThenStepCreatesAS4MessageWithMpcFromSendingPMode()
        {
            var submitMessage = new SubmitMessage
            {
                PMode = DefaultSendPMode()
            };
            submitMessage.Collaboration.AgreementRef = new() { PModeId = submitMessage.PMode.Id };
            submitMessage.MessageInfo.Mpc = null;
            submitMessage.PMode.MessagePackaging.Mpc = "some-mpc";

            var context = new MessagingContext(submitMessage);

            var result = await ExerciseCreateAS4Message(context);

            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
            Assert.Equal("some-mpc", result.MessagingContext.AS4Message.FirstUserMessage.Mpc);
        }

        [Fact]
        public async Task ThenStepCreatesAS4MessageWithGeneratedMessageId()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.MessageInfo.MessageId = null;
            submitMessage.PMode = DefaultSendPMode();
            var internalMessage = new MessagingContext(submitMessage);

            // Act
            var result = await ExerciseCreateAS4Message(internalMessage);

            // Assert
            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
            Assert.NotEmpty(result.MessagingContext.AS4Message.FirstUserMessage.MessageId);
        }

        [Fact]
        public async Task ThenStepCreatesAS4MessageWithAgreement()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.PMode = DefaultSendPMode();
            var internalMessage = new MessagingContext(submitMessage);

            // Act
            var result = await ExerciseCreateAS4Message(internalMessage);

            // Assert
            AssertAgreementReference(submitMessage, result.MessagingContext);
        }

        private static void AssertAgreementReference(SubmitMessage submitMessage, MessagingContext messagingContext)
        {
            Assert.NotNull(submitMessage.PMode);
            Assert.NotNull(submitMessage.PMode.MessagePackaging.CollaborationInfo);
            var pmodeAgreementRef =
                submitMessage.PMode.MessagePackaging.CollaborationInfo.AgreementReference;

            Assert.NotNull(messagingContext.AS4Message);
            Assert.NotNull(messagingContext.AS4Message.FirstUserMessage);
            var userMessageAgreementRef =
                messagingContext.AS4Message.FirstUserMessage.CollaborationInfo.AgreementReference.UnsafeGet;

            Assert.Equal(pmodeAgreementRef.Value, userMessageAgreementRef.Value);
            Assert.Equal(Maybe<string>.Nothing, userMessageAgreementRef.Type);
        }

        [Fact]
        public async Task ThenStepCreatesAS4MessageWithService()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.PMode = DefaultSendPMode();
            var internalMessage = new MessagingContext(submitMessage);

            // Act
            var result = await ExerciseCreateAS4Message(internalMessage);

            // Assert
            Assert.NotNull(submitMessage.PMode);
            Assert.NotNull(submitMessage.PMode.MessagePackaging.CollaborationInfo);
            var pmodService = submitMessage.PMode.MessagePackaging.CollaborationInfo.Service;

            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
            var userMessageService = result.MessagingContext.AS4Message.FirstUserMessage.CollaborationInfo.Service;

            Assert.NotNull(pmodService);
            Assert.NotNull(pmodService.Type);
            Assert.Equal(pmodService.Value, userMessageService.Value);
            Assert.Equal(Maybe.Just(pmodService.Type), userMessageService.Type);
        }

        [Fact]
        public async Task ThenStepCreatesAS4MessageWithAction()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.PMode = DefaultSendPMode();
            var internalMessage = new MessagingContext(submitMessage);

            // Act
            var result = await ExerciseCreateAS4Message(internalMessage);

            // Assert
            Assert.NotNull(submitMessage.PMode);
            Assert.NotNull(submitMessage.PMode.MessagePackaging.CollaborationInfo);
            var pmodeAction = submitMessage.PMode.MessagePackaging.CollaborationInfo.Action;

            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
            var userMessageAction = result.MessagingContext.AS4Message.FirstUserMessage.CollaborationInfo.Action;

            Assert.Equal(pmodeAction, userMessageAction);
        }

        [Fact]
        public async Task ThenStepCreatesAS4MessageWithSenderParty()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.PMode = DefaultSendPMode();
            var internalMessage = new MessagingContext(submitMessage);

            // Act
            var result = await ExerciseCreateAS4Message(internalMessage);

            // Assert
            Assert.NotNull(submitMessage.PMode);
            Assert.NotNull(submitMessage.PMode.MessagePackaging.PartyInfo);
            var pmodeParty = submitMessage.PMode.MessagePackaging.PartyInfo.FromParty;

            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
            var userMessageParty = result.MessagingContext.AS4Message.FirstUserMessage.Sender;

            Assert.NotNull(pmodeParty);
            Assert.Equal(pmodeParty.Role, userMessageParty.Role);
            Assert.Equal(pmodeParty.PrimaryPartyId, userMessageParty.PrimaryPartyId);
        }

        [Fact]
        public async Task ThenStepCreatesAS4MessageWithReceiverParty()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.PMode = DefaultSendPMode();
            var internalMessage = new MessagingContext(submitMessage);

            // Act
            var result = await ExerciseCreateAS4Message(internalMessage);

            // Assert
            Assert.NotNull(submitMessage.PMode);
            Assert.NotNull(submitMessage.PMode.MessagePackaging.PartyInfo);
            var pmodeParty = submitMessage.PMode.MessagePackaging.PartyInfo.ToParty;

            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
            var userMessageParty = result.MessagingContext.AS4Message.FirstUserMessage.Receiver;

            Assert.NotNull(pmodeParty);
            Assert.Equal(pmodeParty.Role, userMessageParty.Role);
            Assert.Equal(pmodeParty.PrimaryPartyId, userMessageParty.PrimaryPartyId);
        }

        [Fact]
        public async Task ThenStepCreatesAS4MessageWithSubmitMessageProperties()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.PMode = DefaultSendPMode();
            var internalMessage = new MessagingContext(submitMessage);

            // Act
            var result = await ExerciseCreateAS4Message(internalMessage);

            // Assert
            AssertMessageProperty(submitMessage, result.MessagingContext);
        }

        private static void AssertMessageProperty(SubmitMessage submitMessage, MessagingContext messagingContext)
        {
            var submitMessageProperty = submitMessage.MessageProperties[0];

            Assert.NotNull(messagingContext.AS4Message);
            Assert.NotNull(messagingContext.AS4Message.FirstUserMessage);
            var userMessageMessageProperty = messagingContext.AS4Message.FirstUserMessage.MessageProperties.First();

            Assert.Equal(submitMessageProperty.Value, userMessageMessageProperty.Value);
            Assert.Equal(submitMessageProperty.Name, userMessageMessageProperty.Name);
        }

        [Fact]
        public async Task CreatesAS4MessageWithOnlySendingPModeProperties()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.MessageProperties = [];
            submitMessage.PMode = DefaultSendPMode();
            submitMessage.PMode.MessagePackaging.MessageProperties =
                [
                    new() { Name = "originalSender", Value = "Holodeck" },
                    new() { Name = "finalRecipient", Value = "AS4.NET" },
                ];

            // Act
            var result = await ExerciseCreateAS4Message(new MessagingContext(submitMessage));

            // Assert
            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
            Assert.Collection(
                result.MessagingContext.AS4Message.FirstUserMessage.MessageProperties,
                p => Assert.Equal((p.Name, p.Value), ("originalSender", "Holodeck")),
                p => Assert.Equal((p.Name, p.Value), ("finalRecipient", "AS4.NET")));
        }
    }

    public class GivenInvalidArguments : GivenCreateAS4MessageStepFacts
    {
        [Fact]
        public async Task ThenStepFailsToCreateAS4MessageWhenSubmitMessageTriesToOVerrideSenderPartyAsync()
        {
            // Arrange
            var submitMessage = SubmitWithTwoPayloads();
            submitMessage.PartyInfo = CreatePopulatedSubmitPartyInfo();
            submitMessage.PMode = DefaultSendPMode();
            var internalMessage = new MessagingContext(submitMessage);

            // Act / Assert
            await Assert.ThrowsAnyAsync<Exception>(() => ExerciseCreateAS4Message(internalMessage));
        }

        private static PartyInfo CreatePopulatedSubmitPartyInfo()
        {
            return new PartyInfo { ToParty = new AS4.Model.Common.Party(), FromParty = new AS4.Model.Common.Party() };
        }
    }

    protected static SubmitMessage SubmitWithTwoPayloads()
    {
        return AS4XmlSerializer.FromString<SubmitMessage>(Properties.Resources.submitmessage)!;
    }

    protected static SendingProcessingMode DefaultSendPMode()
    {
        return AS4XmlSerializer.FromString<SendingProcessingMode>(Properties.Resources.sendingprocessingmode)!;
    }

    protected static async Task<StepResult> ExerciseCreateAS4Message(MessagingContext context)
    {
        var sut = new CreateAS4MessageStep(
            Substitute.For<ILogger<CreateAS4MessageStep>>(),
            StubPayloadRetrieverProvider.Instance,
            Default.SubmitMessageValidator,
            Default.SubmitMessageMap);

        // Act
        return await sut.ExecuteAsync(context, CancellationToken.None);
    }
}
