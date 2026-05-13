using System.Security.Cryptography.X509Certificates;
using System.Text;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Send;
using Eu.EDelivery.AS4.UnitTests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send;

/// <summary>
/// Testing <see cref="EncryptAS4MessageStep" />
/// </summary>
public class GivenEncryptAS4MessageStepFacts
{
    private EncryptAS4MessageStep _step;

    public GivenEncryptAS4MessageStepFacts()
    {
        var certificateRepositoryMock = CreateStubCertificateRepository();

        _step = new EncryptAS4MessageStep(NullLogger<EncryptAS4MessageStep>.Instance, certificateRepository: certificateRepositoryMock.Object);
    }

    private static Mock<ICertificateRepository> CreateStubCertificateRepository()
    {
        var certificateRepositoryMock = new Mock<ICertificateRepository>();

        certificateRepositoryMock.Setup(x => x.GetCertificate(It.IsAny<X509FindType>(), It.IsAny<string>()))
                                 .Returns(new StubCertificateRepository().GetStubCertificate());
        return certificateRepositoryMock;
    }

    public class GivenValidArguments : GivenEncryptAS4MessageStepFacts
    {
        [Fact]
        public async Task ThenExecuteStepSucceedsAsync()
        {
            // Act
            var stepResult = await ExerciseEncryption(CreateEncryptedAS4Message());

            // Assert
            Assert.NotNull(stepResult.MessagingContext.AS4Message);
            Assert.True(stepResult.MessagingContext.AS4Message.IsEncrypted);
        }

        [Fact]
        public async Task EncryptedDatasHasTakenOverAttachmentInfo()
        {
            // Arrange
            var message = CreateEncryptedAS4Message();
            var expectedMimeType = message.AS4Message!.Attachments.First().ContentType;

            var result = await ExerciseEncryption(message);

            // Assert
            var actualMimeType = FirstEncryptedDataMimeTypeAttributeValue(result);

            Assert.Equal(expectedMimeType, actualMimeType);
            Assert.All(message.AS4Message.Attachments, a => Assert.Equal("application/octet-stream", a.ContentType));
        }

        private static async Task<StepResult> ExerciseEncryption(MessagingContext message)
        {
            var sut = new EncryptAS4MessageStep(NullLogger<EncryptAS4MessageStep>.Instance, certificateRepository: CreateStubCertificateRepository().Object);

            // Act
            return await sut.ExecuteAsync(message, CancellationToken.None);
        }

        private static string? FirstEncryptedDataMimeTypeAttributeValue(StepResult result)
        {
            var node = result.MessagingContext.AS4Message!.SecurityHeader.GetXml()!;
            var attachmentNode = node.SelectSingleNode("//*[local-name()='EncryptedData']");

            return attachmentNode?.Attributes?.GetNamedItem("MimeType")?.Value;
        }
    }

    public class GivenInvalidArguments : GivenEncryptAS4MessageStepFacts
    {
        [Fact]
        public async Task ThenExecuteStepFailsWithInvalidCertificateAsync()
        {
            // Arrange
            var certificateRepositoryMock = CreateFailedMockedCertificateRepository();
            _step = new EncryptAS4MessageStep(NullLogger<EncryptAS4MessageStep>.Instance, certificateRepository: certificateRepositoryMock.Object);

            // Act / Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => _step.ExecuteAsync(CreateEncryptedAS4Message(), CancellationToken.None));
        }

        private static Mock<ICertificateRepository> CreateFailedMockedCertificateRepository()
        {
            var certificateRepositoryMock = new Mock<ICertificateRepository>();

            certificateRepositoryMock.Setup(x => x.GetCertificate(It.IsAny<X509FindType>(), It.IsAny<string>()))
                                     .Throws(new Exception("Invalid Certificate"));

            return certificateRepositoryMock;
        }
    }

    protected static MessagingContext CreateEncryptedAS4Message()
    {
        Stream attachmentStream = new MemoryStream(Encoding.UTF8.GetBytes("Hello, encrypt me"));
        var attachment = new Attachment("attachment-id", attachmentStream, "text/plain");

        var as4Message = AS4Message.Empty;
        as4Message.AddMessageUnit(new UserMessage($"user-{Guid.NewGuid()}"));
        as4Message.AddAttachment(attachment);

        var message = new MessagingContext(as4Message, MessagingContextMode.Unknown)
        {
            SendingPMode = new SendingProcessingMode()
        };

        message.SendingPMode.Security.Encryption.IsEnabled = true;
        message.SendingPMode.Security.Encryption.Algorithm = "http://www.w3.org/2009/xmlenc11#aes128-gcm";
        message.SendingPMode.Security.Encryption.CertificateType = PublicKeyCertificateChoiceType.CertificateFindCriteria;
        message.SendingPMode.Security.Encryption.EncryptionCertificateInformation = new CertificateFindCriteria()
        {
            CertificateFindType = X509FindType.FindBySerialNumber,
            CertificateFindValue = "some dummy value"
        };

        return message;
    }
}
