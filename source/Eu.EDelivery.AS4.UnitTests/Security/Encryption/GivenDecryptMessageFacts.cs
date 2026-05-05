using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Security.Encryption;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;
using static Eu.EDelivery.AS4.Constants.Namespaces;

namespace Eu.EDelivery.AS4.UnitTests.Security.Encryption;

public class GivenDecryptMessageFacts
{
    [Fact]
    public async Task DecryptMultipleImagePayloadsCorrectly()
    {
        // Arrange
        var as4Message = await GetEncryptedMessageAsync();
        var decryptCertificate = GetDecryptCertificate();

        // Act
        as4Message.Decrypt(decryptCertificate);

        // Assert
        Assert.Equal(flower1, GetAttachmentContents(as4Message.Attachments.ElementAt(0)));
        Assert.Equal(flower2, GetAttachmentContents(as4Message.Attachments.ElementAt(1)));
        Assert.All(
            as4Message.Attachments,
            a => Assert.Equal("image/jpeg", a.ContentType));
    }

    //[Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "Creates as4message for DecryptMultipleImagePayloadsCorrectly")]
    public static async Task CreateAs4EncryptedMessage()
    {
        var message = new UserMessage(
            messageId: "30392f3c-bc9c-4a1f-ba5e-5d4d81382eef@CLT-SNEIRINCK",
            collaboration: CollaborationInfo.DefaultTest,
            sender: new("Sender", new PartyId(EbmsDefaultFrom)),
            receiver: new("Receiver", new PartyId(EbmsDefaultTo)),
            partInfos: [
                new($"cid:30392f3c-bc9c-4a1f-ba5e-5d4d81382ee-1427821804@CLT-SNEIRINCK"),
                new($"cid:30392f3c-bc9c-4a1f-ba5e-5d4d81382ee-1136335733@CLT-SNEIRINCK")],
            messageProperties: []
            );
        var attachment1 = new Attachment("30392f3c-bc9c-4a1f-ba5e-5d4d81382ee-1427821804@CLT-SNEIRINCK", new MemoryStream(flower1), "image/jpeg");
        var attachment2 = new Attachment("30392f3c-bc9c-4a1f-ba5e-5d4d81382ee-1136335733@CLT-SNEIRINCK", new MemoryStream(flower2), "image/jpeg");

        var as4Message = AS4Message.Create(message);
        as4Message.AddAttachment(attachment1);
        as4Message.AddAttachment(attachment2);
        as4Message.EnvelopeDocument = AS4XmlSerializer.ToSoapEnvelopeDocument(as4Message);
        as4Message.Encrypt(new(new(AccessPointB_cer)), DataEncryptionConfiguration.Default);

        using var output = File.Create("as4_encrypted_message");
        await Default.MimeMessageSerializer.SerializeAsync(as4Message, output, CancellationToken.None);
    }

    [Fact]
    public async Task DecryptSetsCompressedContentTypeForPayloadsAfterwards()
    {
        // Arrange
        var as4Message = await GetEncryptedCompressedMessageAsync();
        var decryptCert = GetDecryptCertificate();

        // Act
        as4Message.Decrypt(decryptCert);

        // Assert
        Assert.All(
            as4Message.Attachments,
            a => Assert.Equal("application/gzip", a.ContentType));
    }

    [Fact]
    public async Task DecryptUnmarksSecurityHeaderAsEncrypted()
    {
        // Arrange
        var as4Message = await GetEncryptedMessageAsync();
        var decryptCertificate = GetDecryptCertificate();

        Assert.True(as4Message.IsEncrypted);

        // Act
        as4Message.Decrypt(decryptCertificate);

        Assert.False(as4Message.IsEncrypted);
    }

    [Fact]
    public async Task DecryptRemovesEncryptionElementsFromSecurityHeader()
    {
        // Arrange
        var as4Message = await GetEncryptedMessageAsync();
        var decryptCertificate = GetDecryptCertificate();

        // Act
        as4Message.Decrypt(decryptCertificate);

        // Assert
        var encryptedKeyNode = as4Message.SecurityHeader?.GetXml()?.SelectSingleNode("//*[local-name()='EncryptedKey']");
        Assert.Null(encryptedKeyNode);

        var encryptedDatas = as4Message.SecurityHeader?.GetXml()?.SelectNodes("//*[local-name()='EncryptedData']");
        Assert.True(encryptedDatas == null || encryptedDatas.Count == 0);
    }

    [Fact]
    public async Task DecryptFailsWhenWrongDecryptionCertificateIsGiven()
    {
        // Arrange
        var as4Message = await GetEncryptedMessageAsync();
        var certificate = new StubCertificateRepository().GetStubCertificate();

        // Act&Assert
        Assert.ThrowsAny<Exception>(() => as4Message.Decrypt(certificate));
    }

    private static Task<AS4Message> GetEncryptedMessageAsync()
    {
        return DeserializeEncryptedMessageAsync(
            as4_encrypted_message,
            "multipart/related; boundary=\"MIMEBoundary_64ed729f813b10a65dfdc363e469e2206ff40c4aa5f4bd11\"");
    }

    private static Task<AS4Message> GetEncryptedCompressedMessageAsync()
    {
        return DeserializeEncryptedMessageAsync(
            as4_encrypted_compressed_message,
            "multipart/related; boundary=\"=-6sJmyirLVoAPyUJUzCWk0w==\"");
    }

    private static async Task<AS4Message> DeserializeEncryptedMessageAsync(byte[] contents, string contentType)
    {
        var inputStr = new MemoryStream(contents);

        var output = await Default.SerializerProvider
            .Get(contentType)
            .DeserializeAsync(inputStr, contentType, CancellationToken.None);

        Assert.True(output.IsEncrypted, "The AS4Message to use in this testcase should be encrypted");
        Assert.All(output.Attachments, a => Assert.Equal("application/octet-stream", a.ContentType));

        return output;
    }

    private static X509Certificate2 GetDecryptCertificate()
    {
        return new X509Certificate2(
            rawData: holodeck_partyc_certificate,
            password: "ExampleC",
            X509KeyStorageFlags.Exportable);
    }

    private static byte[] GetAttachmentContents(Attachment attachment)
    {
        var attachmentInMemory = new MemoryStream();
        attachment.Content.CopyTo(attachmentInMemory);
        attachment.ResetContentPosition();
        return attachmentInMemory.ToArray();
    }

    [Fact]
    public async Task DecryptsKeyIdentifierSignedAS4Message()
    {
        // Arrange
        const string ContentType =
            "multipart/related; boundary=\"MIMEBoundary_e26ba07b9ac392cc88fdb1c2ed23ba5e2e6d64fdf13325f1\"; type=\"application/soap+xml\"; start=\"<0.116ba07b9ac392cc88fdb1c2ed23ba5e2e6d64fdf13325f1@apache.org>\"";

        var encrypted = await Default.SerializerProvider
            .Get(ContentType)
            .DeserializeAsync(
                new MemoryStream(as4_encrypted_signed_keyidentifier),
                ContentType,
                CancellationToken.None);

        // Act
        encrypted.Decrypt(
            new X509Certificate2(
                AccessPointB_pfx,
                AccessPointB_password,
                X509KeyStorageFlags.Exportable));

        // Assert
        Assert.False(encrypted.IsEncrypted);
    }

    [Fact]
    public async Task DecryptsIssuerSerialSignedAS4Message()
    {
        // Arrange
        const string ContentType =
            "multipart/related; boundary=\"MIMEBoundary_416ba07b9ac392cc88fdb1c2ed23ba5e2e6d64fdf13325f1\"; type=\"application/soap+xml\"; start=\"<0.716ba07b9ac392cc88fdb1c2ed23ba5e2e6d64fdf13325f1@apache.org>\"";

        var encrypted = await Default.SerializerProvider
            .Get(ContentType)
            .DeserializeAsync(
                new MemoryStream(as4_encrypted_signed_issuerserial),
                ContentType,
                CancellationToken.None);

        // Act
        encrypted.Decrypt(
            new X509Certificate2(
                AccessPointB_pfx,
                AccessPointB_password,
                X509KeyStorageFlags.Exportable));

        // Assert
        Assert.False(encrypted.IsEncrypted);
    }
}
