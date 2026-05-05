using System.Text;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Streaming;
using Eu.EDelivery.AS4.Transformers;
using Eu.EDelivery.AS4.UnitTests.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using CollaborationInfo = Eu.EDelivery.AS4.Model.Core.CollaborationInfo;
using Party = Eu.EDelivery.AS4.Model.Core.Party;
using PartyId = Eu.EDelivery.AS4.Model.Core.PartyId;
using Service = Eu.EDelivery.AS4.Model.Core.Service;

namespace Eu.EDelivery.AS4.UnitTests.Transformers;

/// <summary>
/// Testing <see cref="DeliverMessageTransformer"/>
/// </summary>
public class GivenDeliverMessageTransformerFacts
{
    [Fact]
    public async Task CreateDeliverMessagesFromUserMessages()
    {
        // Arrange
        var partId1 = $"part-{Guid.NewGuid()}";
        var userMessage1 = new UserMessage(
            $"user-{Guid.NewGuid()}",
            new CollaborationInfo(
                new Service($"service-{Guid.NewGuid()}"),
                $"action-{Guid.NewGuid()}"),
            new Party("Sender", new PartyId($"id-{Guid.NewGuid()}")),
            new Party("Receiver", new PartyId($"id-{Guid.NewGuid()}")),
            [new PartInfo($"cid:{partId1}")],
            []);

        var partId2 = $"part-{Guid.NewGuid()}";
        var userMessage2 = new UserMessage(
            $"user-{Guid.NewGuid()}",
            new CollaborationInfo(
                new Service($"service-{Guid.NewGuid()}"),
                $"action-{Guid.NewGuid()}"),
            new Party("Sender", new PartyId($"id-{Guid.NewGuid()}")),
            new Party("Receiver", new PartyId($"id-{Guid.NewGuid()}")),
            [new PartInfo($"cid:{partId2}")],
            []);

        var as4Message = AS4Message.Create([userMessage1, userMessage2]);
        as4Message.AddAttachment(new Attachment(partId1));
        as4Message.AddAttachment(new Attachment(partId2));

        var receivingPMode = new ReceivingProcessingMode { Id = "deliver-pmode" };
        var entity1 = new InMessage(userMessage1.MessageId);
        entity1.SetPModeInformation(receivingPMode);
        var entity2 = new InMessage(userMessage2.MessageId);
        entity2.SetPModeInformation(receivingPMode);

        var sut = new DeliverMessageTransformer(NullLogger<DeliverMessageTransformer>.Instance, Default.AS4MessageTransformer);

        // Act
        var result1 = await sut.TransformAsync(new ReceivedEntityMessage(entity1, as4Message.ToStream(), as4Message.ContentType), CancellationToken.None);

        var result2 = await sut.TransformAsync(new ReceivedEntityMessage(entity2, as4Message.ToStream(), as4Message.ContentType), CancellationToken.None);

        // Assert
        Assert.NotNull(result1.DeliverMessage);
        var mappingFailures1 = DeliverMessageOriginateFrom(
            userMessage1,
            receivingPMode,
            result1.DeliverMessage.Message);
        Assert.Empty(mappingFailures1);

        Assert.NotNull(result2.DeliverMessage);
        var mappingFailures2 = DeliverMessageOriginateFrom(
            userMessage2,
            receivingPMode,
            result2.DeliverMessage.Message);
        Assert.Empty(mappingFailures2);
    }

    private static IEnumerable<string> DeliverMessageOriginateFrom(
        UserMessage user,
        ReceivingProcessingMode receivingPMode,
        DeliverMessage deliver)
    {
        if (user.MessageId != deliver.MessageInfo.MessageId)
        {
            yield return "MessageId";
        }

        if (user.CollaborationInfo.Service.Value != deliver.CollaborationInfo?.Service?.Value)
        {
            yield return "Service";
        }

        if (user.CollaborationInfo.Action != deliver.CollaborationInfo?.Action)
        {
            yield return "Action";
        }

        if (user.Sender.PrimaryPartyId != deliver.PartyInfo?.FromParty?.PartyIds?.FirstOrDefault()?.Id)
        {
            yield return "FromParty";
        }

        if (user.Receiver.PrimaryPartyId != deliver.PartyInfo?.ToParty?.PartyIds?.FirstOrDefault()?.Id)
        {
            yield return "ToParty";
        }

        if (receivingPMode.Id != deliver.CollaborationInfo?.AgreementRef?.PModeId)
        {
            yield return "PModeId";
        }
    }

    [Fact]
    public async Task FailsToTransformIfNoUserMessageCanBeFound()
    {
        // Arrange
        var sut = new DeliverMessageTransformer(NullLogger<DeliverMessageTransformer>.Instance, Default.AS4MessageTransformer);
        var receivedMessage = CreateReceivedMessage(receivedInMessageId: "ignored id", as4Message: AS4Message.Empty);

        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(() => sut.TransformAsync(receivedMessage, CancellationToken.None));
    }

    [Fact]
    public async Task FailsToTransformIfInvalidMessageEntityHasGiven()
    {
        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(() => new DeliverMessageTransformer(NullLogger<DeliverMessageTransformer>.Instance, Default.AS4MessageTransformer)
           .TransformAsync(new ReceivedMessage(VirtualStream.Create()), CancellationToken.None));
    }

    [Fact]
    public async Task TransformRemovesUnnecessaryAttachments()
    {
        // Arrange
        const string ExpectedId = "usermessage-id";
        const string ExpectedUri = "expected-attachment-uri";

        var user = new UserMessage(ExpectedId, new PartInfo("cid:" + ExpectedUri));
        var message = AS4Message.Create(user);
        message.AddAttachment(FilledAttachment(ExpectedUri));
        message.AddAttachment(FilledAttachment());
        message.AddAttachment(FilledAttachment());

        // Act
        var actualMessage = await ExerciseTransform(ExpectedId, message);

        // Assert
        Assert.NotNull(actualMessage.DeliverMessage);
        Assert.Single(actualMessage.DeliverMessage.Attachments);
    }

    private static Attachment FilledAttachment(string? attachmentId = null)
    {
        return new Attachment(
            id: attachmentId ?? Guid.NewGuid().ToString(),
            content: new MemoryStream(Encoding.UTF8.GetBytes("serialize me!")),
            contentType: "text/plain");
    }

    private static async Task<MessagingContext> ExerciseTransform(string expectedId, AS4Message as4Message)
    {
        var receivedMessage = CreateReceivedMessage(receivedInMessageId: expectedId, as4Message: as4Message);
        var sut = new DeliverMessageTransformer(NullLogger<DeliverMessageTransformer>.Instance, Default.AS4MessageTransformer);

        return await sut.TransformAsync(receivedMessage, CancellationToken.None);
    }

    private static ReceivedEntityMessage CreateReceivedMessage(string receivedInMessageId, AS4Message as4Message)
    {
        var inMessage = new InMessage(receivedInMessageId);

        return new ReceivedEntityMessage(inMessage, as4Message.ToStream(), as4Message.ContentType);
    }
}
