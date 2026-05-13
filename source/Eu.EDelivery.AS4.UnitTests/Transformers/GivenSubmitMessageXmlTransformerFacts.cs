using System.Text;
using System.Xml.Serialization;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Transformers;
using Microsoft.Extensions.Logging.Abstractions;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;
using CollaborationInfo = Eu.EDelivery.AS4.Model.Common.CollaborationInfo;

namespace Eu.EDelivery.AS4.UnitTests.Transformers;

/// <summary>
/// Testing the <see cref="SubmitMessageXmlTransformer" />
/// </summary>
public class GivenSubmitMessageXmlTransformerFacts
{
    [Fact]
    public async Task ThenPModeIsNotPartOfTheSerializationAsync()
    {
        // Arrange
        var submitMessage = new SubmitMessage
        {
            Collaboration = { AgreementRef = new() { PModeId = "this-pmode-id" } },
            PMode = new SendingProcessingMode { Id = "other-pmode-id" }
        };

        var receivedMessage = CreateMessageFrom(submitMessage);

        // Act
        var messagingContext = await Transform(receivedMessage);

        // Assert
        Assert.NotNull(messagingContext.SubmitMessage);
        Assert.Null(messagingContext.SubmitMessage.PMode);

        await receivedMessage.UnderlyingStream.DisposeAsync();
    }

    [Fact]
    public async Task ThenTransformSucceedsWithPModeIdAsync()
    {
        // Arrange
        const string ExpectedPModeId = "01-pmode";
        var submitMessage = new SubmitMessage
        {
            Collaboration = new CollaborationInfo { AgreementRef = new Agreement { PModeId = ExpectedPModeId } }
        };

        var receivedMessage = CreateMessageFrom(submitMessage);

        // Act
        var messagingContext = await Transform(receivedMessage);

        // Assert
        Assert.NotNull(messagingContext.SubmitMessage);
        Assert.Equal(ExpectedPModeId, messagingContext.SubmitMessage.Collaboration.AgreementRef?.PModeId);

        await receivedMessage.UnderlyingStream.DisposeAsync();
    }

    [Fact]
    public async Task TransformInvalidXmlInputFailsDeserializing()
    {
        await Assert.AllAsync(
            [
                "<Invalid-XML>",
                submitmessage_invalid_messageproperties,
                submitmessage_missing_payload_location,
                submitmessage_missing_schema_location,
                submitmessage_missing_payload_property_name,
                submitmessage_missing_collaboration,
                submitmessage_missing_collaboration_agreement,
                submitmessage_missing_collaboration_agreement_pmodeid
            ], async x =>
            {
                // Arrange
                var messageStream = new MemoryStream(Encoding.UTF8.GetBytes(x));
                var receivedMessage = new ReceivedMessage(messageStream);

                // Act / Assert
                await Assert.ThrowsAnyAsync<Exception>(async () => await Transform(receivedMessage));
            });
    }

    protected static async Task<MessagingContext> Transform(ReceivedMessage message)
    {
        return await new SubmitMessageXmlTransformer(NullLogger<SubmitMessageXmlTransformer>.Instance).TransformAsync(message, CancellationToken.None);
    }

    private static ReceivedMessage CreateMessageFrom(SubmitMessage submitMessage)
    {
        return new ReceivedMessage(WriteSubmitMessageToStream(submitMessage));
    }

    private static MemoryStream WriteSubmitMessageToStream(SubmitMessage submitMessage)
    {
        var memoryStream = new MemoryStream();
        var serializer = new XmlSerializer(typeof(SubmitMessage));

        serializer.Serialize(memoryStream, submitMessage);
        memoryStream.Position = 0;

        return memoryStream;
    }
}
