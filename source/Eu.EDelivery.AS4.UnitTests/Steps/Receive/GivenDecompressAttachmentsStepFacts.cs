using System.IO.Compression;
using System.Text;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PartInfo = Eu.EDelivery.AS4.Model.Core.PartInfo;
using UserMessage = Eu.EDelivery.AS4.Model.Core.UserMessage;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Receive;

/// <summary>
/// Testing the <see cref="DecompressAttachmentsStep" />
/// </summary>
public class GivenDecompressAttachmentsStepFacts
{
    public class GivenValidArguments : GivenDecompressAttachmentsStepFacts
    {
        [CustomProperty]
        public void BundledUserMessageWithSignalGetsDecompressed(SignalMessage signal)
        {
            // Arrange
            var attachmentId = $"attachment-{Guid.NewGuid()}";
            var user = UserMessageWithCompressedInfo(attachmentId);
            var attachment = CompressedAttachment(attachmentId);
            var as4Message = AS4Message.Create(user);
            as4Message.AddMessageUnit(signal);
            as4Message.AddAttachment(attachment);

            // Act
            var result = ExerciseDecompressInternal(as4Message);

            // Assert
            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.All(
                result.MessagingContext.AS4Message.Attachments,
                a => Assert.NotEqual("application/gzip", a.ContentType));

            Assert.All(
                result.MessagingContext.AS4Message.UserMessages.SelectMany(u => u.PayloadInfo),
                p => Assert.Equal("application/gzip", p.CompressionType));
        }

        [Property]
        public Property MultipleUserMessagesTheirAttachmentsGetsDecompressed(NonEmptyArray<Guid> attachmentIds)
        {
            Action act = () =>
            {
                // Arrange
                var as4Message = attachmentIds.Get.Distinct().Aggregate(
                    AS4Message.Empty,
                    (as4, id) =>
                    {
                        as4.AddMessageUnit(UserMessageWithCompressedInfo(id.ToString()));
                        as4.AddAttachment(CompressedAttachment(id.ToString()));
                        return as4;
                    });

                // Act
                var result = ExerciseDecompressInternal(as4Message);

                // Assert
                Assert.NotNull(result.MessagingContext.AS4Message);
                Assert.All(
                    result.MessagingContext.AS4Message.Attachments,
                    a => Assert.NotEqual("application/gzip", a.ContentType));

                Assert.All(
                    result.MessagingContext.AS4Message.UserMessages.SelectMany(u => u.PayloadInfo),
                    p => Assert.Equal("application/gzip", p.CompressionType));
            };

            return act.When(attachmentIds.Get.Distinct().Any());
        }

        private static StepResult ExerciseDecompressInternal(AS4Message as4Message)
        {
            var sut = new DecompressAttachmentsStep(NullLogger<DecompressAttachmentsStep>.Instance, Default.CompressStrategy);
            return sut.ExecuteAsync(new MessagingContext(as4Message, MessagingContextMode.Receive), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        [Fact]
        public async Task ThenExecuteSucceedsWithValidAttachments()
        {
            // Arrange
            var context = CompressedAS4Message();

            // Act
            var stepResult = await ExerciseDecompress(context);

            // Assert
            Assert.NotNull(stepResult.MessagingContext.AS4Message);
            Assert.NotNull(stepResult.MessagingContext.AS4Message.Attachments.First().Content);
        }

        [Fact]
        public async Task ThenExecuteSucceedsWithNoCompressedAttachment()
        {
            // Arrange
            var context = CompressedAS4Message();
            var first = context.AS4Message!.Attachments.First();
            first.UpdateContent(first.Content, "not supported MIME type");

            // Act
            var stepResult = await ExerciseDecompress(context);

            // Assert
            Assert.NotNull(stepResult.MessagingContext.AS4Message);
            Assert.All(
                stepResult.MessagingContext.AS4Message.Attachments,
                a => Assert.NotEqual("application/gzip", a.ContentType));
        }
    }

    public class GivenInvalidArguments : GivenDecompressAttachmentsStepFacts
    {
        [Fact]
        public async Task ThenExecuteFailsWithMissingMimTypePartProperty()
        {
            // Arrange
            var context = CompressedAS4Message();
            var attachment = context.AS4Message!.Attachments.First();
            attachment.Properties.Remove("MimeType");

            // Act
            var result = await ExerciseDecompress(context);

            // Assert
            var error = result.MessagingContext.ErrorResult;
            Assert.NotNull(error);
            Assert.Equal(ErrorCode.Ebms0303, error.Code);
        }
    }

    private static MessagingContext CompressedAS4Message()
    {
        const string AttachmentId = "attachment-id";

        var as4Message = AS4Message.Create(UserMessageWithCompressedInfo(AttachmentId));
        as4Message.AddAttachment(CompressedAttachment(AttachmentId));

        return new MessagingContext(as4Message, MessagingContextMode.Unknown);
    }

    private static Attachment CompressedAttachment(string attachmentId)
    {
        var memoryStream = new MemoryStream();
        var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress);
        var input = new MemoryStream(Encoding.UTF8.GetBytes("Dummy Attachment Content"));
        input.CopyTo(gzipStream);

        memoryStream.Position = 0;

        return new Attachment(
            attachmentId,
            memoryStream,
            "application/gzip",
            new Dictionary<string, string> { ["MimeType"] = "html/text" });
    }

    private static UserMessage UserMessageWithCompressedInfo(string attachmentId)
    {
        var partInfo = new PartInfo(
            href: "cid:" + attachmentId,
            properties: new Dictionary<string, string> { ["MimeType"] = "html/text" },
            schemas: []);

        return new UserMessage($"user-{Guid.NewGuid()}", partInfo);
    }

    private static async Task<StepResult> ExerciseDecompress(MessagingContext context)
    {
        var sut = new DecompressAttachmentsStep(NullLogger<DecompressAttachmentsStep>.Instance, Default.CompressStrategy);

        // Act
        return await sut.ExecuteAsync(context, CancellationToken.None);
    }
}
