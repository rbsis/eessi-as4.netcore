using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive;
using Microsoft.Extensions.Logging.Abstractions;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Receive;

public class GivenValidateAS4MessageStepFacts
{
    [Fact]
    public async Task ValidationFailureIfExternalPayloadReference()
    {
        // Arrange
        var attachment = new Attachment("earth", Stream.Null, "text/plain");
        var user = new UserMessage(
            $"user-{Guid.NewGuid()}",
            new PartInfo("earth"));

        var message = AS4Message.Create(user);
        message.AddAttachment(attachment);
        message = await SerializeDeserialize(message);

        // Act
        var result = await ExerciseValidation(message);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.MessagingContext.ErrorResult);
        Assert.Equal(ErrorCode.Ebms0011, result.MessagingContext.ErrorResult.Code);
    }

    [Fact]
    public async Task ValidationFailureIfNoAttachmentCanBeFoundForEachPartInfo()
    {
        // Arrange
        var attachment = new Attachment("earth", Stream.Null, "text/plain");
        var user = new UserMessage($"user-{Guid.NewGuid()}", new PartInfo("cid:some other href"));
        var message = AS4Message.Create(user);
        message.AddAttachment(attachment);
        message = await SerializeDeserialize(message);

        // Act
        var result = await ExerciseValidation(message);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.MessagingContext.ErrorResult);
        Assert.Equal(ErrorCode.Ebms0009, result.MessagingContext.ErrorResult.Code);
    }

    [Fact]
    public async Task ValidationFailureIfSoapBodyAttachmentFound()
    {
        const string ContentType = "application/soap+xml";
        var message = await BuildMessageFor(as4_soapattachment, ContentType);

        var result = await ExerciseValidation(message);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.MessagingContext.ErrorResult);
        Assert.Equal(ErrorAlias.FeatureNotSupported, result.MessagingContext.ErrorResult.Alias);
    }

    [Fact]
    public async Task ValidationSucceedsIfSoapBodyHasNoAttachment()
    {
        const string ContentType = "multipart/related; boundary=\"=-M9awlqbs/xWAPxlvpSWrAg==\"; type=\"application/soap+xml\"; charset=\"utf-8\"";
        var message = await BuildMessageFor(System.Text.Encoding.UTF8.GetBytes(as4message), ContentType);

        var result = await ExerciseValidation(message);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ValidationFailureIfUserMessageContainsDuplicatePayloadIds()
    {
        var user = new UserMessage(
            $"user-{Guid.NewGuid()}",
            CollaborationInfo.DefaultTest,
            Party.DefaultFrom,
            Party.DefaultTo,
            [
                new PartInfo("cid:earth1"),
                new PartInfo("cid:earth2"),
            ],
            []);

        var message = AS4Message.Create(user);
        message.AddAttachment(new Attachment("earth1", Stream.Null, "text/plain"));
        message = await SerializeDeserialize(message);

        var result = await ExerciseValidation(message);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.MessagingContext.ErrorResult);
        Assert.Equal(ErrorAlias.InvalidHeader, result.MessagingContext.ErrorResult.Alias);
    }

    private static async Task<AS4Message> SerializeDeserialize(AS4Message message)
    {
        var serializer = Default.MimeMessageSerializer;

        var memory = new MemoryStream();
        await serializer.SerializeAsync(message, memory, CancellationToken.None);
        memory.Position = 0;

        return await serializer.DeserializeAsync(memory, message.ContentType, CancellationToken.None);
    }

    private static async Task<AS4Message> BuildMessageFor(byte[] as4MessageExternalPayloads, string contentType)
    {
        using var stream = new MemoryStream(as4MessageExternalPayloads);
        return await Default.MimeMessageSerializer.DeserializeAsync(stream, contentType, CancellationToken.None);
    }

    private static async Task<StepResult> ExerciseValidation(AS4Message message)
    {
        var sut = new ValidateAS4MessageStep(NullLogger<ValidateAS4MessageStep>.Instance);

        return await sut.ExecuteAsync(new MessagingContext(message, MessagingContextMode.Receive), CancellationToken.None);
    }
}

