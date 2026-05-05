using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Security.Encryption;
using Eu.EDelivery.AS4.Serialization;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;

namespace Eu.EDelivery.AS4.UnitTests.Security.Encryption;

public class GivenEncryptAndDecryptMessageFacts
{
    [Fact]
    public void ThenEncryptEncryptsAndDecryptDecryptsTheAttachmentsCorrectly()
    {
        // Arrange
        var as4Message = CreateAS4Message();
        var originalAttachment1Length = as4Message.Attachments.ElementAt(0).Content.Length;
        var originalAttachment2Length = as4Message.Attachments.ElementAt(1).Content.Length;

        // Act
        as4Message.Encrypt(
            new KeyEncryptionConfiguration(CreateEncryptCertificate()),
            DataEncryptionConfiguration.Default);

        // Assert
        Assert.True(as4Message.IsEncrypted);
        Assert.NotEqual(originalAttachment1Length, as4Message.Attachments.ElementAt(0).Content.Length);
        Assert.NotEqual(originalAttachment2Length, as4Message.Attachments.ElementAt(1).Content.Length);
        Assert.NotEqual([flower1, flower2], as4Message.Attachments.Select(GetAttachmentContents));

        // Arrange
        as4Message.EnvelopeDocument = AS4XmlSerializer.ToSoapEnvelopeDocument(as4Message);

        // Act
        as4Message.Decrypt(CreateDecryptCertificate());

        // Assert
        Assert.False(as4Message.IsEncrypted);
        Assert.Equal(originalAttachment1Length, as4Message.Attachments.ElementAt(0).Content.Length);
        Assert.Equal(originalAttachment2Length, as4Message.Attachments.ElementAt(1).Content.Length);
        Assert.Equal([flower1, flower2], as4Message.Attachments.Select(GetAttachmentContents));
    }

    private static AS4Message CreateAS4Message()
    {
        var attachment1 = new Attachment("attachment-1", new MemoryStream(flower1), "image/jpeg");
        var attachment2 = new Attachment("attachment-2", new MemoryStream(flower2), "image/jpeg");

        var as4Message = AS4Message.Empty;
        as4Message.AddAttachment(attachment1);
        as4Message.AddAttachment(attachment2);
        as4Message.EnvelopeDocument = AS4XmlSerializer.ToSoapEnvelopeDocument(as4Message);

        return as4Message;
    }

    private static X509Certificate2 CreateEncryptCertificate() => new(AccessPointB_cer);

    private static X509Certificate2 CreateDecryptCertificate() => new(
        AccessPointB_pfx,
        AccessPointB_password,
        X509KeyStorageFlags.Exportable);

    private static byte[] GetAttachmentContents(Attachment attachment)
    {
        var attachmentInMemory = new MemoryStream();
        attachment.Content.CopyTo(attachmentInMemory);
        attachment.ResetContentPosition();
        return attachmentInMemory.ToArray();
    }
}
