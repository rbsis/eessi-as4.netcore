using System.Text;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Send;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send;

/// <summary>
/// Testing <seealso cref="CompressAttachmentsStep" />
/// </summary>
public class GivenCompressAttachmentsStepFacts
{
    [Fact]
    public async Task DoesntCompressAttachmentsIfPModeIsNotSetForCompression()
    {
        // Arrange
        var expected = NonCompressedAttachment();
        var context = AS4MessageContext(expected, PModeWithoutCompressionSettings());

        // Act
        var result = await ExerciseCompression(context);

        // Assert
        Assert.NotNull(result.MessagingContext.AS4Message);
        var actual = result.MessagingContext.AS4Message.Attachments.First();

        Assert.Equal(expected.Content, actual.Content);
        Assert.NotEqual("application/gzip", actual.ContentType);
    }

    private static SendingProcessingMode PModeWithoutCompressionSettings()
    {
        return new SendingProcessingMode { MessagePackaging = { UseAS4Compression = false } };
    }

    [Fact]
    public async Task SucceedsToCompressAttachmentIfPModeIsSetForCompression()
    {
        // Arrange
        var nonCompressedAttachment = NonCompressedAttachment();
        var expectedLength = nonCompressedAttachment.Content.Length;
        var expectedType = nonCompressedAttachment.ContentType;

        var context = AS4MessageContext(nonCompressedAttachment, PModeWithCompressionSettings());

        // Act
        var result = await ExerciseCompression(context);

        // Assert
        Assert.NotNull(result.MessagingContext.AS4Message);
        var message = result.MessagingContext.AS4Message;
        var actual = message.Attachments.First();

        Assert.NotEqual(expectedLength, actual.Content.Length);
        Assert.Equal(expectedType, actual.Properties["MimeType"]);
        Assert.Equal("application/gzip", actual.ContentType);
        Assert.All(
            message.UserMessages.SelectMany(u => u.PayloadInfo),
            p => Assert.Equal("application/gzip", p.CompressionType));
    }

    private static Attachment NonCompressedAttachment()
    {
        return new Attachment(
            id: "attachment-id",
            content: new MemoryStream(Encoding.UTF8.GetBytes("compress me!")),
            contentType: "text/plain");
    }

    private static MessagingContext AS4MessageContext(Attachment attachment, SendingProcessingMode pmode)
    {
        var userMessage = new UserMessage($"user-{Guid.NewGuid()}", PartInfo.CreateFor(attachment));
        var as4Message = AS4Message.Create(userMessage, pmode);
        as4Message.AddAttachment(attachment);

        return new MessagingContext(as4Message, MessagingContextMode.Unknown) { SendingPMode = pmode };
    }

    private static SendingProcessingMode PModeWithCompressionSettings()
    {
        return new SendingProcessingMode { MessagePackaging = { UseAS4Compression = true } };
    }

    private static async Task<StepResult> ExerciseCompression(MessagingContext context)
    {
        var sut = new CompressAttachmentsStep(
            Substitute.For<ILogger<CompressAttachmentsStep>>(),
            Default.CompressStrategy);

        // Act
        return await sut.ExecuteAsync(context, CancellationToken.None);
    }
}
