using System.Text;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Transformers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Transformers;

/// <summary>
/// Testing <see cref="PayloadTransformer" />
/// </summary>
public class GivenPayloadTransformerFacts
{
    public class GivenValidArguments : GivenPayloadTransformerFacts
    {
        [Fact]
        public async Task ThenTransformSucceedsWithValidStreamAsync()
        {
            // Arrange
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("Transform me!"));
            const string ContentType = "text/plain";
            var receivedMessage = new ReceivedMessage(stream, ContentType);

            // Act
            var messagingContext = await new PayloadTransformer(NullLogger<PayloadTransformer>.Instance).TransformAsync(receivedMessage, CancellationToken.None);

            // Assert
            Assert.NotNull(messagingContext.AS4Message);
            var firstAttachment = messagingContext.AS4Message.Attachments.First();
            Assert.Equal(ContentType, firstAttachment.ContentType);
            Assert.Equal(stream, firstAttachment.Content);
        }
    }
}
