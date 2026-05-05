using System.Security.Cryptography.X509Certificates;
using System.Text;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Security.Encryption;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;

namespace Eu.EDelivery.AS4.UnitTests.Security.Encryption;

public class GivenEncryptMessageFacts
{
    [Fact]
    public void ThenEncryptEncryptsTheAttachmentsCorrectly()
    {
        // Arrange
        var as4Message = CreateAS4Message();
        var originalAttachmentLength = as4Message.Attachments.First().Content.Length;

        // Act
        as4Message.Encrypt(new KeyEncryptionConfiguration(CreateEncryptionCertificate()),
                           DataEncryptionConfiguration.Default);


        // Assert
        Assert.True(as4Message.IsEncrypted);

        var firstAttachment = as4Message.Attachments.ElementAt(0);
        Assert.NotEqual(originalAttachmentLength, firstAttachment?.Content.Length);
    }

    [Fact]
    public void FailsToEncryptIfInvalidKeySize()
    {
        // Arrange
        var as4Message = CreateAS4Message();

        var keyEncryptionConfig = new KeyEncryptionConfiguration(CreateEncryptionCertificate());
        var dataEncryptionConfig = new DataEncryptionConfiguration(AS4.Model.PMode.Encryption.Default.Algorithm, -1);

        // Act / Assert
        Assert.ThrowsAny<Exception>(() => as4Message.Encrypt(keyEncryptionConfig, dataEncryptionConfig));
    }

    private static AS4Message CreateAS4Message()
    {
        var attachmentContents = Encoding.UTF8.GetBytes("hi!");
        var attachment = new Attachment("attachment-id", new MemoryStream(attachmentContents), "text/plain");

        var as4Message = AS4Message.Create(pmode: null);
        as4Message.AddAttachment(attachment);

        return as4Message;
    }

    private static X509Certificate2 CreateEncryptionCertificate()
    {
        // TODO: we should just have a public key certificate here, without the need to specify the password.
        return new X509Certificate2(
            holodeck_partyc_certificate,
            "ExampleC");
    }
}