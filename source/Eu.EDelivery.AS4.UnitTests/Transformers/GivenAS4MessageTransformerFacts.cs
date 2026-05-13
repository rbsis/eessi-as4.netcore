using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Transformers;
using Eu.EDelivery.AS4.UnitTests.Extensions;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;

namespace Eu.EDelivery.AS4.UnitTests.Transformers;

/// <summary>
/// Testing the <see cref="AS4MessageTransformer" />
/// </summary>
public class GivenAS4MessageTransformerFacts
{
    /// <summary>
    /// Testing if the Transformer succeeds
    /// for the "Transform" Method
    /// </summary>
    public class GivenValidReceivedMessageToTransformer : GivenAS4MessageTransformerFacts
    {
        [Fact]
        public async Task ThenTransfromSuceedsWithAS4Message()
        {
            // Arrange
            const string ContentType = "multipart/related; boundary=\"=-PHQq1fuE9QxpIWax7CKj5w==\"; type=\"application/soap+xml\"; charset=\"utf-8\"";

            // Act
            var context = await ExerciseTransform(as4_single_payload, ContentType);

            // Assert
            Assert.NotNull(context?.AS4Message);

            // TearDown
            context.Dispose();
        }

        private static async Task<MessagingContext> ExerciseTransform(byte[] contents, string contentType)
        {
            var stream = new MemoryStream(contents);
            var receivedMessage = new ReceivedMessage(stream, contentType);

            return await Transform(receivedMessage);
        }
    }

    /// <summary>
    /// Testing if the Transformer fails
    /// for the "Transform" Method
    /// </summary>
    public class GivenInvalidArgumentsToTransfrormer : GivenAS4MessageTransformerFacts
    {
        [Fact]
        public async Task ThenTransformFailsWithInvalidUserMessageWithSoapAS4StreamAsync()
        {
            // Arrange
            var as4Message = CreateAS4MessageWithoutAttachments();
            as4Message.AddMessageUnit(new UserMessage("message-id"));
            var memoryStream = as4Message.ToStream();

            var receivedMessage = new ReceivedMessage(memoryStream, Constants.ContentTypes.Mime);

            // Act / Assert
            await Assert.ThrowsAnyAsync<Exception>(() => Transform(receivedMessage));
        }

        [Fact]
        public async Task ThenTransformFailsIfContentIsNotSupported()
        {
            // Arrange
            var saboteurMessage = new ReceivedMessage(Stream.Null, "not-supported-content-type");

            // Act / Assert
            await Assert.ThrowsAnyAsync<Exception>(() => Transform(saboteurMessage));
        }

        [Fact]
        public async Task ThenTransformFailsIfRequestStreamIsNull()
        {
            // Arrange
            var saboteurMessage = new ReceivedMessage(underlyingStream: Stream.Null);

            // Act / Assert
            await Assert.ThrowsAnyAsync<Exception>(() => Transform(saboteurMessage));
        }

        private static AS4Message CreateAS4MessageWithoutAttachments()
        {
            var userMessage = new UserMessage(
                "message-id",
                new Party("Sender", new PartyId(Guid.NewGuid().ToString())),
                new Party("Receiver", new PartyId(Guid.NewGuid().ToString())));

            return AS4Message.Create(userMessage);
        }
    }

    protected static async Task<MessagingContext> Transform(ReceivedMessage message)
    {
        var transformer = new AS4MessageTransformer(Default.SerializerProvider);
        return await transformer.TransformAsync(message, CancellationToken.None);
    }
}
