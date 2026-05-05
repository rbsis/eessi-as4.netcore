using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Submit;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Eu.EDelivery.AS4.UnitTests.Model.PMode;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Party = Eu.EDelivery.AS4.Model.PMode.Party;
using PartyId = Eu.EDelivery.AS4.Model.PMode.PartyId;
using PartyInfo = Eu.EDelivery.AS4.Model.PMode.PartyInfo;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Submit;

public class GivenCreateAS4MessageStepFacts
{
    [Fact]
    public async Task CanCreateMessageWithPModeWithoutToParty()
    {
        // Arrange
        var sendingParty = CreatePModeParty("sender", "c2", "eu.edelivery.services");

        var pmode = CreateSendingPMode(fromParty: sendingParty, toParty: null);

        var receivingParty = CreateSubmitMessageParty("receiver", "type", "c3");

        var submitMessage = CreateSubmitMessage(pmode, fromParty: null, toParty: receivingParty);

        var context = new MessagingContext(submitMessage) { SendingPMode = pmode };

        // Act
        var result = await ExerciseCreation(context);

        // Assert
        Assert.True(result.Succeeded);
        var as4Message = result.MessagingContext.AS4Message;

        Assert.NotNull(as4Message);
        Assert.False(as4Message.IsEmpty);
        Assert.True(as4Message.IsUserMessage);
        Assert.NotNull(as4Message.FirstUserMessage);
        Assert.Equal(receivingParty.Role, as4Message.FirstUserMessage.Receiver.Role);
        Assert.NotNull(receivingParty.PartyIds);
        Assert.Equal(receivingParty.PartyIds[0].Id, as4Message.FirstUserMessage.Receiver.PartyIds.First().Id);
        Assert.Equal(receivingParty.PartyIds[0].Type, as4Message.FirstUserMessage.Receiver.PartyIds.First().Type.UnsafeGet);
    }

    [Fact]
    public async Task CanCreateMessageWithPModeWithoutFromParty()
    {
        // Arrange
        var receivingParty = CreatePModeParty("receiver", "c3", "eu.edelivery.services");

        var pmode = CreateSendingPMode(fromParty: null, toParty: receivingParty);

        var fromParty = CreateSubmitMessageParty("sender", "type", "c2");

        var submitMessage = CreateSubmitMessage(pmode, fromParty: fromParty, toParty: null);

        var context = new MessagingContext(submitMessage) { SendingPMode = pmode };

        // Act
        var result = await ExerciseCreation(context);

        // Assert
        Assert.True(result.Succeeded);
        var as4Message = result.MessagingContext.AS4Message;

        Assert.NotNull(as4Message);
        Assert.False(as4Message.IsEmpty);
        Assert.True(as4Message.IsUserMessage);
        Assert.NotNull(as4Message.FirstUserMessage);
        Assert.Equal(fromParty.Role, as4Message.FirstUserMessage.Sender.Role);
        Assert.NotNull(fromParty.PartyIds);
        Assert.Equal(fromParty.PartyIds[0].Id, as4Message.FirstUserMessage.Sender.PartyIds.First().Id);
        Assert.Equal(fromParty.PartyIds[0].Type, as4Message.FirstUserMessage.Sender.PartyIds.First().Type.UnsafeGet);
    }

    [Fact]
    public async Task MessageIsCreatedWithDefaultSenderIfNoneIsSpecified()
    {
        // Arrange
        var receivingParty = CreatePModeParty("receiver", "c3", "eu.edelivery.services");

        var pmode = CreateSendingPMode(fromParty: null, toParty: receivingParty);

        var submitMessage = CreateSubmitMessage(pmode, fromParty: null, toParty: null);

        var context = new MessagingContext(submitMessage) { SendingPMode = pmode };

        // Act
        var result = await ExerciseCreation(context);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.MessagingContext.AS4Message);
        Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
        Assert.Equal(Constants.Namespaces.EbmsDefaultFrom, result.MessagingContext.AS4Message.FirstUserMessage.Sender.PartyIds.First().Id);
        Assert.Equal(Constants.Namespaces.EbmsDefaultRole, result.MessagingContext.AS4Message.FirstUserMessage.Sender.Role);
    }

    [Fact]
    public async Task MessageIsCreatedWithDefaultReceiverIfNoneIsSpecified()
    {
        // Arrange
        var pmode = CreateSendingPMode(fromParty: null, toParty: null);

        var submitMessage = CreateSubmitMessage(pmode, fromParty: null, toParty: null);

        var context = new MessagingContext(submitMessage) { SendingPMode = pmode };

        // Act
        var result = await ExerciseCreation(context);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.MessagingContext.AS4Message);
        Assert.NotNull(result.MessagingContext.AS4Message.FirstUserMessage);
        Assert.Equal(Constants.Namespaces.EbmsDefaultTo, result.MessagingContext.AS4Message.FirstUserMessage.Receiver.PartyIds.First().Id);
        Assert.Equal(Constants.Namespaces.EbmsDefaultRole, result.MessagingContext.AS4Message.FirstUserMessage.Receiver.Role);
    }

    [Fact]
    public async Task MessageIsCreatedWithMessageProperties()
    {
        // Arrange
        var pmode = CreateSendingPMode(fromParty: null, toParty: null);

        var submitMessage = CreateSubmitMessage(pmode, fromParty: null, toParty: null);
        submitMessage.MessageProperties =
        [
            new AS4.Model.Common.MessageProperty("originalSender","unregistered:C1"),
            new AS4.Model.Common.MessageProperty("finalRecipient","unregistered:C2")
        ];

        var context = new MessagingContext(submitMessage) { SendingPMode = pmode };

        // Act
        var result = await ExerciseCreation(context);
        var as4Message = result.MessagingContext.AS4Message;

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(as4Message);
        Assert.NotNull(as4Message.FirstUserMessage);
        Assert.Equal(2, as4Message.FirstUserMessage.MessageProperties.Count());
        Assert.Equal("unregistered:C1", as4Message.FirstUserMessage.MessageProperties.FirstOrDefault(p => p.Name.Equals("originalSender"))?.Value);
        Assert.Equal("unregistered:C2", as4Message.FirstUserMessage.MessageProperties.FirstOrDefault(p => p.Name.Equals("finalRecipient"))?.Value);
    }

    [Fact]
    public async Task MessageIsntCreatedIfDuplicatePayloadIdsAreFound()
    {
        // Arrange
        var pmode = ValidSendingPModeFactory.Create();
        var submit = new SubmitMessage
        {
            PMode = pmode,
            Payloads =
            [
                new Payload("earth", "location", "mime"),
                new Payload("earth", "location", "mime")
            ]
        };
        var context = new MessagingContext(submit) { SendingPMode = pmode };

        // Act / Assert
        await Assert.ThrowsAsync<InvalidMessageException>(() => ExerciseCreation(context));
    }

    private static Party CreatePModeParty(string role, string id, string type)
    {
        return new Party(role, new PartyId { Id = id, Type = type });
    }

    private static AS4.Model.Common.Party CreateSubmitMessageParty(string role, string type, string id)
    {
        return new AS4.Model.Common.Party { Role = role, PartyIds = [new AS4.Model.Common.PartyId(id, type),] };
    }

    private static SubmitMessage CreateSubmitMessage(
        SendingProcessingMode pmode,
        AS4.Model.Common.Party? fromParty,
        AS4.Model.Common.Party? toParty)
    {
        return new SubmitMessage
        {
            Collaboration =
            {
                AgreementRef = new()
                {
                    Value = "submit-agreement",
                    PModeId = "not empty pmode id"
                }
            },
            PartyInfo = new AS4.Model.Common.PartyInfo
            {
                FromParty = fromParty,
                ToParty = toParty
            },
            PMode = pmode
        };
    }

    private static SendingProcessingMode CreateSendingPMode(Party? fromParty, Party? toParty)
    {
        var pmode = ValidSendingPModeFactory.Create();

        pmode.MessagePackaging = new SendMessagePackaging
        {
            PartyInfo = new PartyInfo
            {
                FromParty = fromParty,
                ToParty = toParty
            }
        };

        return pmode;
    }

    private static async Task<StepResult> ExerciseCreation(MessagingContext context)
    {
        var sut = new CreateAS4MessageStep(
            Substitute.For<ILogger<CreateAS4MessageStep>>(),
            StubPayloadRetrieverProvider.Instance,
            Default.SubmitMessageValidator,
            Default.SubmitMessageMap);

        return await sut.ExecuteAsync(context, CancellationToken.None);
    }
}
