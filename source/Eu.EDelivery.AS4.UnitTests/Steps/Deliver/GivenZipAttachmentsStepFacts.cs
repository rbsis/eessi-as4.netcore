using System.Text;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps.Deliver;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Deliver;

/// <summary>
/// Testing <see cref="ZipAttachmentsStep" />
/// </summary>
public class GivenZipAttachmentsStepFacts
{

    public class GivenValidArguments : GivenZipAttachmentsStepFacts
    {
        [Fact]
        public async Task ThenStepWillNotZipSingleAttachment()
        {
            // Arrange
            const string ContentType = "image/png";

            var as4Message = AS4Message.Empty;
            as4Message.AddAttachment(new Attachment("attachment-id", Stream.Null, ContentType));

            // Act
            await new ZipAttachmentsStep(NullLogger<ZipAttachmentsStep>.Instance).ExecuteAsync(
                new MessagingContext(as4Message, MessagingContextMode.Unknown), CancellationToken.None);

            // Assert
            Assert.Collection(
                as4Message.Attachments,
                a => Assert.Equal(ContentType, a.ContentType));
        }

        [Fact]
        public async Task ThenStepWillZipMultipleAttachments()
        {
            // Arrange
            var as4Message = AS4MessageWithTwoAttachments();

            // Act
            await new ZipAttachmentsStep(NullLogger<ZipAttachmentsStep>.Instance).ExecuteAsync(
                new MessagingContext(as4Message, MessagingContextMode.Unknown), CancellationToken.None);

            // Assert
            Assert.Collection(
                as4Message.Attachments,
                a => Assert.Equal("application/zip", a.ContentType));
        }

        private static AS4Message AS4MessageWithTwoAttachments()
        {
            static Attachment CreateAttachment()
            {
                return new Attachment(
                    id: $"attachment{Guid.NewGuid()}",
                    content: new MemoryStream(Encoding.UTF8.GetBytes("Plain Dummy Text")),
                    contentType: "text/plain");
            }

            var message = AS4Message.Empty;
            message.AddAttachment(CreateAttachment());
            message.AddAttachment(CreateAttachment());

            return message;
        }
    }
}
