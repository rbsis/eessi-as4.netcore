using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Receive;

/// <summary>
/// Testing <see cref="DecryptAS4MessageStep" />
/// </summary>
public class GivenDecryptAS4MessageStepFacts
{
    public class GivenValidArguments : GivenDecryptAS4MessageStepFacts
    {
        [Fact]
        public async Task DecryptBundeldMessageCorrectly()
        {
            // Arrange
            var as4Message = await GetBundledEncryptedMessageAsync();

            // Act
            var result = await ExerciseDecryption(as4Message);

            // Assert
            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.False(result.MessagingContext.AS4Message.IsEncrypted);
        }

        private static async Task<AS4Message> GetBundledEncryptedMessageAsync()
        {
            var bundled = await DeserializeToEncryptedMessage(
                as4_bundled_encrypted_message,
                "multipart/related; boundary=\"MIMEBoundary_64ed729f813b10a65dfdc363e469e2206ff40c4aa5f4bd11\"");

            Assert.True(
                bundled.MessageUnits.Count() > 1,
                "Encrypted AS4Message was expected to be bundled (more than a single MessageUnit)");

            return bundled;
        }

        [Fact]
        public async Task ThenExecuteStepSucceedsAsync()
        {
            // Arrange
            var as4Message = await GetEncryptedAS4MessageAsync();
            var context = new MessagingContext(as4Message, MessagingContextMode.Receive)
            {
                ReceivingPMode = new ReceivingProcessingMode
                {
                    Security = {Decryption =
                    {
                        Encryption = Limit.Allowed,
                        DecryptCertificateInformation = new CertificateFindCriteria
                        {
                            CertificateFindType = X509FindType.FindByIssuerName,
                            CertificateFindValue = ""
                        }
                    }}
                }
            };

            // Act
            var stepResult = await ExerciseDecryption(context);

            // Assert
            Assert.NotNull(stepResult.MessagingContext.AS4Message);
            Assert.False(stepResult.MessagingContext.AS4Message.IsEncrypted);
        }

        [Fact]
        public async Task TestIfAttachmentContentTypeIsSetBackToOriginal()
        {
            // Arrange
            var as4Message = await GetEncryptedAS4MessageAsync();
            var context = new MessagingContext(as4Message, MessagingContextMode.Receive)
            {
                ReceivingPMode = new ReceivingProcessingMode
                {
                    Security = {Decryption =
                    {
                        Encryption = Limit.Allowed,
                        DecryptCertificateInformation = new CertificateFindCriteria
                        {
                            CertificateFindType =  X509FindType.FindBySerialNumber,
                            CertificateFindValue = ""
                        }
                    }}
                }
            };

            // Act
            var result = await ExerciseDecryption(context);

            // Assert
            Assert.NotNull(result.MessagingContext.AS4Message);
            var attachments = result.MessagingContext.AS4Message.Attachments;
            Assert.All(attachments, a => Assert.Equal("image/jpeg", a.ContentType));
        }
    }

    public class GivenInvalidArguments : GivenDecryptAS4MessageStepFacts
    {
        [Fact]
        public async Task ThenExecuteStepFailsWithNotAllowedEncryptionAsync()
        {
            // Arrange
            var as4Message = await CreateEncryptedAS4Message();
            var internalMessage = new MessagingContext(as4Message, MessagingContextMode.Receive)
            {
                ReceivingPMode = new ReceivingProcessingMode
                {
                    Security = { Decryption = { Encryption = Limit.NotAllowed } }
                }
            };

            // Act
            var result = await ExerciseDecryption(internalMessage);

            // Assert
            Assert.False(result.Succeeded);

            var error = result.MessagingContext.ErrorResult;
            Assert.NotNull(error);
            Assert.Equal(ErrorCode.Ebms0103, error.Code);
        }

        private static async Task<AS4Message> CreateEncryptedAS4Message()
        {
            var message = AS4Message.Create(new UserMessage("somemessage"));
            message.AddAttachment(
                new Attachment(
                    "some-attachment",
                    Stream.Null,
                    "text/plain"));

            var encryptedMessage =
                AS4MessageUtils.EncryptWithCertificate(
                    message, new StubCertificateRepository().GetStubCertificate());

            return await Default.SerializerProvider
                .SerializeDeserializeAsync(encryptedMessage, CancellationToken.None);
        }

        [Fact]
        public async Task ThenExecuteStepFailsWithRequiredEncryptionAsync()
        {
            // Arrange
            var context = new MessagingContext(AS4Message.Empty, MessagingContextMode.Receive)
            {
                ReceivingPMode = new ReceivingProcessingMode
                {
                    Security = { Decryption = { Encryption = Limit.Required } }
                }
            };

            // Act
            var result = await ExerciseDecryption(context);

            // Assert
            var error = result.MessagingContext.ErrorResult;
            Assert.NotNull(error);
            Assert.Equal(ErrorCode.Ebms0103, error.Code);
        }

        [Fact]
        public async Task DecryptFailsWhenAttachmentIsntReferencedByEncryptedData()
        {
            // Arrange
            var m =
                await DeserializeToEncryptedMessage(
                    as4_soap_wrong_encrypted_no_encrypteddata_for_attachment,
                    "multipart/related; boundary=\"MIMEBoundary_64ed729f813b10a65dfdc363e469e2206ff40c4aa5f4bd11\"");

            // Act
            var result = await ExerciseDecryption(
                new MessagingContext(m, MessagingContextMode.Receive)
                {
                    ReceivingPMode = ReceivingPModeForDecryption()
                });

            // Assert
            Assert.False(result.CanProceed);
            Assert.NotNull(result.MessagingContext.ErrorResult);
            Assert.Equal(ErrorAlias.FailedDecryption, result.MessagingContext.ErrorResult.Alias);
        }
    }

    private static async Task<AS4Message> DeserializeToEncryptedMessage(byte[] messageContents, string contentType)
    {
        var inputStream = new MemoryStream(messageContents);

        var message = await Default.MimeMessageSerializer.DeserializeAsync(
            inputStream,
            contentType,
            CancellationToken.None);

        Assert.True(message.IsEncrypted, "The AS4 Message to use in this testcase should be encrypted");

        return message;
    }

    private static Task<StepResult> ExerciseDecryption(MessagingContext ctx)
    {
        var mockedRespository = new Mock<ICertificateRepository>();

        mockedRespository
            .Setup(r => r.GetCertificate(It.IsAny<X509FindType>(), It.IsAny<string>()))
            .Returns(new X509Certificate2(
                rawData: holodeck_partyc_certificate,
                password: "ExampleC",
                keyStorageFlags: X509KeyStorageFlags.Exportable));

        var sut = new DecryptAS4MessageStep(NullLogger<DecryptAS4MessageStep>.Instance, certificateRepository: mockedRespository.Object);
        return sut.ExecuteAsync(ctx, CancellationToken.None);
    }

    private static Task<StepResult> ExerciseDecryption(AS4Message msg)
    {
        var mockedRespository = new Mock<ICertificateRepository>();

        mockedRespository
            .Setup(r => r.GetCertificate(It.IsAny<X509FindType>(), It.IsAny<string>()))
            .Returns(new X509Certificate2(
                         rawData: holodeck_partyc_certificate,
                         password: "ExampleC",
                         keyStorageFlags: X509KeyStorageFlags.Exportable));

        var sut = new DecryptAS4MessageStep(NullLogger<DecryptAS4MessageStep>.Instance, certificateRepository: mockedRespository.Object);
        return sut.ExecuteAsync(
            new MessagingContext(msg, MessagingContextMode.Receive)
            {
                ReceivingPMode = ReceivingPModeForDecryption()
            }, CancellationToken.None);
    }

    private static ReceivingProcessingMode ReceivingPModeForDecryption() => new()
    {
        Security =
        {
            Decryption =
            {
                Encryption = Limit.Required,
                CertificateType = PrivateKeyCertificateChoiceType.PrivateKeyCertificate,
                DecryptCertificateInformation = new CertificateFindCriteria
                {
                    CertificateFindType = X509FindType.FindBySubjectName,
                    CertificateFindValue = "ExampleC"
                }
            }
        }
    };

    [Fact]
    public async Task TestEncryptedMessageIfAttachmentsAreCorrectlyDeserialized()
    {
        // Act
        var sut = await GetEncryptedAS4MessageAsync();

        // Assert
        Assert.True(sut.HasAttachments, "Deserialized message hasn't got any attachments");
        Assert.All(sut.Attachments, a => Assert.Equal("application/octet-stream", a.ContentType));
    }

    protected static Task<AS4Message> GetEncryptedAS4MessageAsync()
    {
        var inputStream = new MemoryStream(as4_encrypted_message);

        return Default.MimeMessageSerializer.DeserializeAsync(
            inputStream,
            "multipart/related; boundary=\"MIMEBoundary_64ed729f813b10a65dfdc363e469e2206ff40c4aa5f4bd11\"",
            CancellationToken.None);
    }
}
