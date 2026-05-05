using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;

namespace Eu.EDelivery.AS4.UnitTests.Factories;
public class GivenPayloadFileNameFactoryFacts
{
    [Fact]
    public void DefaultPatternIsAttachmentIdPattern()
    {
        var payloadId = "earth.jpg";
        var attachment = new Attachment(payloadId);
        var userMessage = new MessageInfo("messageId");

        var payloadFileName = PayloadFileNameFactory.CreateFileName(null, attachment, userMessage);

        Assert.Equal(payloadId, payloadFileName);
    }

    [Fact]
    public void ThenGenerateFileNameWithCombinedPattern()
    {
        var attachment = new Attachment("earth.jpg");
        var userMessage = new MessageInfo("messageId");

        var payloadFileName = PayloadFileNameFactory.CreateFileName("{MessageId}_{AttachmentId}", attachment, userMessage);

        Assert.Equal($"{userMessage.MessageId}_{attachment.Id}", payloadFileName);
    }

    [Fact]
    public void ThenAppendAttachmentIdIfPatternContainsNoMacro()
    {
        var attachment = new Attachment("earth.jpg");
        var userMessage = new MessageInfo("messageId");

        var pattern = "abc_";

        var payloadFileName = PayloadFileNameFactory.CreateFileName(pattern, attachment, userMessage);

        Assert.Equal($"abc_{attachment.Id}", payloadFileName);
    }
}
