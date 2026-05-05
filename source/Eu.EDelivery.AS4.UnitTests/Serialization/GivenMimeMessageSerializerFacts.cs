using System.Text;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Serialization;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;

namespace Eu.EDelivery.AS4.UnitTests.Serialization;

/// <summary>
/// Testing the <see cref="MimeMessageSerializer" />
/// </summary>
public class GivenMimeMessageSerializerFacts
{
    private const string AnonymousContentType =
        "multipart/related; boundary=\"=-M9awlqbs/xWAPxlvpSWrAg==\"; type=\"application/soap+xml\"; charset=\"utf-8\"";

    protected static async Task<AS4Message> ExerciseMimeDeserialize(Stream stream, string contentType)
    {
        // Arrange
        var sut = new MimeMessageSerializer(NullLogger<MimeMessageSerializer>.Instance, new SoapEnvelopeSerializer());

        // Act
        return await sut.DeserializeAsync(stream, contentType, CancellationToken.None);
    }

    public class GivenMimeMessageSerializerSucceeds : GivenMimeMessageSerializerFacts
    {
        [Fact]
        public async Task DeserializeMultiHopSignalMessage()
        {
            // Arrange
            const string ContentType =
                "multipart/related; boundary=\"=-M/sMGEhQK8RBNg/21Nf7Ig==\";\ttype=\"application/soap+xml\"";
            var messageString = Encoding.UTF8.GetString(as4_multihop_message).Replace((char)0x1F, ' ');
            var messageContent = Encoding.UTF8.GetBytes(messageString);

            using var messageStream = new MemoryStream(messageContent);
            // Act
            var actualMessage = await ExerciseMimeDeserialize(messageStream, ContentType);

            // Assert
            Assert.True(actualMessage.IsSignalMessage);
        }

        [Fact]
        public async Task ThenAttachmentContentTypeIsNotNullAsync()
        {
            // Act
            var as4Message = await ExerciseMimeDeserializeAnonymousUserMessage();

            // Assert
            Assert.NotNull(as4Message);
            Assert.Equal(2, as4Message.Attachments.Count());
        }

        [Fact]
        public async Task ThenDeserializeAS4MessageSucceedsForContentTypeAsync()
        {
            // Act
            var as4Message = await ExerciseMimeDeserializeAnonymousUserMessage();

            // Assert
            Assert.Equal(AnonymousContentType, as4Message.ContentType);
            Assert.Contains(Constants.ContentTypes.Mime, as4Message.ContentType);
        }

        private static async Task<AS4Message> ExerciseMimeDeserializeAnonymousUserMessage()
        {
            using var messageStream = SerializeAnonymousMessage();
            return await ExerciseMimeDeserialize(messageStream, AnonymousContentType);
        }

        [Fact]
        public void ThenSerializeAS4MessageSucceeds()
        {
            using var messageStream = SerializeAnonymousMessage();
            // Arrange
            var as4Message = CreateAnonymousMessage();
            var sut = new MimeMessageSerializer(NullLogger<MimeMessageSerializer>.Instance, new SoapEnvelopeSerializer());

            // Act
            sut.Serialize(as4Message, messageStream);

            // Assert
            Assert.True(messageStream.CanRead);
            Assert.True(messageStream.Length > 0);
        }

        private static AS4Message CreateAnonymousMessage()
        {
            var message = AS4Message.Create(new UserMessage("message-id"));
            message.AddAttachment(CreateEarthAttachment());

            return message;
        }

        private static Attachment CreateEarthAttachment()
        {
            return new Attachment(
                id: "attachment-id",
                content: new MemoryStream(Encoding.UTF8.GetBytes("attachment-stream")),
                contentType: "text/plain");
        }

        [Property]
        public static void ThenSerializeWithAttachmentsReturnsMimeMessage(NonEmptyString messageContents)
        {
            // Arrange
            var attachmentStream = new MemoryStream(Encoding.UTF8.GetBytes(messageContents.Get));
            var attachment = new Attachment("attachment-id", attachmentStream, "text/plain");

            var userMessage = new UserMessage("message-id");

            var message = AS4Message.Create(userMessage);
            message.AddAttachment(attachment);

            // Act
            AssertMimeMessageIsValid(message);
        }

        private static void AssertMimeMessageIsValid(AS4Message message)
        {
            using var mimeStream = new MemoryStream();
            var mimeMessage = SerializeMimeMessage(message, mimeStream);
            var envelopeStream = mimeMessage.BodyParts.OfType<MimePart>().First().Content!.Open();
            var rawXml = new StreamReader(envelopeStream).ReadToEnd();

            // Assert
            Assert.NotNull(rawXml);
            Assert.Contains("Envelope", rawXml);
        }

        private static MimeMessage SerializeMimeMessage(AS4Message message, Stream mimeStream)
        {
            ISerializer serializer = new MimeMessageSerializer(NullLogger<MimeMessageSerializer>.Instance, new SoapEnvelopeSerializer());
            serializer.Serialize(message, mimeStream);

            mimeStream.Position = 0;

            return MimeMessage.Load(mimeStream);
        }
    }

    public class GivenMimeMessageSerializerFails : GivenMimeMessageSerializerFacts
    {
        [Fact]
        public async Task ThenDeserializeFailsWithInvalidContentTypeAsync()
        {
            using var messageStream = SerializeAnonymousMessage();
            const string NotCompleteContentType = Constants.ContentTypes.Mime;

            // Act / Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => ExerciseMimeDeserialize(messageStream, NotCompleteContentType));
        }
    }

    private static Stream SerializeAnonymousMessage()
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(as4message));
    }
}
