using System.Security.Cryptography.X509Certificates;
using System.Text;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Security.Signing;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive;
using Eu.EDelivery.AS4.Streaming;
using Eu.EDelivery.AS4.TestUtils;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Model;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using FsCheck;
using Microsoft.Extensions.Logging.Abstractions;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Receive;

/// <summary>
/// Testing the <see cref="VerifySignatureAS4MessageStep" />
/// </summary>
public class GivenVerifySignatureAS4MessageStepFacts : GivenDatastoreFacts
{
    public class GivenValidArguments : GivenVerifySignatureAS4MessageStepFacts
    {
        [Fact]
        public async Task SucceedsVerifyBundledSignedAS4Message()
        {
            // Arrange
            var user = new UserMessage($"user-{Guid.NewGuid()}");
            var receipt = new Receipt($"receipt-{Guid.NewGuid()}", $"user-{Guid.NewGuid()}");
            var as4Message = AS4Message.Create(user);
            as4Message.AddMessageUnit(receipt);
            var cert = new X509Certificate2(holodeck_partya_certificate, certificate_password, X509KeyStorageFlags.Exportable);
            var signed = AS4MessageUtils.SignWithCertificate(as4Message, cert);
            signed = await SerializeDeserializeSoap(signed);

            var ctx = new MessagingContext(signed, MessagingContextMode.Receive)
            {
                ReceivingPMode = ReceivingPModeWithAllowedSigningVerification()
            };

            // Act
            var result = await ExerciseVerify(ctx);
            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task SucceedsVerifyCorrectSignedUserMessage()
        {
            // Arrange
            var ctx = await DeserializeSignedMessage(as4_soap_signed_message);

            ctx.ReceivingPMode = ReceivingPModeWithAllowedSigningVerification();

            // Act
            var result = await ExerciseVerify(ctx);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.MessagingContext.AS4Message);
            Assert.True(result.MessagingContext.AS4Message.IsSigned);
        }

        [Fact]
        public async Task SucceedsVerifyCorrectSignedReceiptWithMatchingRepudiationHashes()
        {
            // Arrange
            static byte[] EqualHashes(byte[] hashes) => hashes;

            // Act
            var verifyResult = await TestVerifyNRRReceipt(EqualHashes);

            // Assert
            Assert.True(verifyResult.CanProceed);
        }

        [Fact]
        public async Task SucceedsVerifyReceiptWithCorruptRepudiationHashesOnIgnored()
        {
            // Arrange
            static byte[] IncrementedHashes(byte[] hashes) => [.. hashes.Select(i => (byte)(i + 10))];

            // Act
            var verifyResult = await TestVerifyNRRReceipt(IncrementedHashes, verifyNrr: false);

            // Assert
            Assert.True(verifyResult.CanProceed);
        }

        [Fact]
        public async Task SucceedsVerifyReceiptWithCorruptRepudiationHashesIfReceiverIsFinalRecipient()
        {
            // Arrange
            static byte[] CorruptHashes(byte[] hashes) => [.. hashes.Select(i => (byte)(i * 10))];

            // Act
            var verifyResult = await TestVerifyNRRReceipt(CorruptHashes, intermediary: true);

            // Assert
            Assert.True(verifyResult.CanProceed);
        }

        [Fact]
        public async Task TakesSendingPModeIntoAccountWhenVerifiesNonMultihopSignal()
        {
            // Arrange
            var as4Msg = AS4Message.Create(new Receipt($"receipt-{Guid.NewGuid()}", $"reftoid-{Guid.NewGuid()}"));
            as4Msg.AddMessageUnit(new UserMessage(messageId: $"user-{Guid.NewGuid()}"));

            var ctx = new MessagingContext(as4Msg, MessagingContextMode.Receive)
            {
                ReceivingPMode = new ReceivingProcessingMode
                {
                    Security = { SigningVerification = { Signature = Limit.Required } }
                },
                SendingPMode = new SendingProcessingMode
                {
                    Security = { SigningVerification = { Signature = Limit.Ignored } }
                }
            };

            // Act
            var result = await ExerciseVerify(ctx);

            // Assert
            Assert.True(result.CanProceed);
        }

        [Fact]
        public async Task SucceedsWrongSignedSignalMessageButIgnored()
        {
            // Arrange
            var ctx =
                await DeserializeSignedMessage(as4_soap_wrong_signed_pullrequest);
            ctx.SendingPMode = new SendingProcessingMode
            {
                Security = { SigningVerification = { Signature = Limit.Ignored } }
            };

            // Act
            var result = await ExerciseVerify(ctx);

            // Assert
            Assert.True(result.Succeeded);
        }
    }

    public class GivenInvalidArguments : GivenVerifySignatureAS4MessageStepFacts
    {
        [Fact]
        public async Task FailsVerifyUnsignedSignalMessageButRequired()
        {
            // Arrange
            var ctx = SignalMessageWithVerification(Limit.Required);

            // Act
            var result = await ExerciseVerify(ctx);

            // Assert
            Assert.False(result.Succeeded);
            Assert.NotNull(result.MessagingContext.ErrorResult);
            Assert.Equal(ErrorAlias.PolicyNonCompliance, result.MessagingContext.ErrorResult.Alias);
        }

        [Fact]
        public async Task FailsVerifySignedSignalMessageButUnallowed()
        {
            // Arrange
            var ctx = SignalMessageWithVerification(Limit.NotAllowed);

            ctx.AS4Message!.Sign(
                new CalculateSignatureConfig(
                    signingCertificate: new X509Certificate2(
                        rawData: holodeck_partya_certificate,
                        password: certificate_password,
                        keyStorageFlags: X509KeyStorageFlags.Exportable)));

            // Act
            var result = await ExerciseVerify(ctx);

            // Assert
            Assert.False(result.Succeeded);
            Assert.NotNull(result.MessagingContext.ErrorResult);
            Assert.Equal(ErrorAlias.PolicyNonCompliance, result.MessagingContext.ErrorResult.Alias);
        }

        private static MessagingContext SignalMessageWithVerification(Limit sendSignature)
        {
            var signal = AS4Message.Create(new Receipt($"receipt-{Guid.NewGuid()}", $"reftoid-{Guid.NewGuid()}"));

            var ctx = new MessagingContext(signal, MessagingContextMode.Receive)
            {
                SendingPMode = new SendingProcessingMode
                {
                    Security = { SigningVerification = { Signature = sendSignature } }
                }
            };
            return ctx;
        }

        [Fact]
        public async Task FailsVerifyUserMessageWithWrongSignedOnAllowed()
        {
            // Arrange
            var ctx =
                await DeserializeSignedMessage(as4_soap_wrong_signed_message);

            ctx.ReceivingPMode = ReceivingPModeWithAllowedSigningVerification();

            // Act
            var result = await ExerciseVerify(ctx);

            // Assert
            Assert.NotNull(result.MessagingContext.ErrorResult);
            Assert.Equal(ErrorCode.Ebms0101, result.MessagingContext.ErrorResult.Code);
        }

        [Fact]
        public async Task FailsVerifyUserMessageWithUntrustedCertOnAllowed()
        {
            // Arrange
            var ctx =
                await DeserializeSignedMessage(as4_soap_untrusted_signed_message);

            ctx.ReceivingPMode = ReceivingPModeWithAllowedSigningVerification();

            // Act
            var result = await ExerciseVerify(ctx);

            // Assert
            Assert.NotNull(result.MessagingContext.ErrorResult);
            Assert.Equal(ErrorCode.Ebms0101, result.MessagingContext.ErrorResult.Code);
        }

        [Fact]
        public async Task FailsVerifySignalMessageWithCorruptRepidiationHashes()
        {
            // Arrange
            static byte[] ReversedHashes(byte[] hashes) => [.. hashes.Reverse()];

            // Act
            var verifyResult = await TestVerifyNRRReceipt(ReversedHashes);

            // Assert
            Assert.False(verifyResult.CanProceed);
            Assert.NotNull(verifyResult.MessagingContext.ErrorResult);
            Assert.Equal(ErrorCode.Ebms0101, verifyResult.MessagingContext.ErrorResult.Code);
        }
    }

    protected async Task<StepResult> TestVerifyNRRReceipt(
        Func<byte[], byte[]> adaptHashes,
        bool verifyNrr = true,
        bool intermediary = false)
    {
        // Arrange
        const string MessageId = "verify-nrr-message-id";

        var signedUserMessage = await SignedUserMessage(MessageId);
        InsertOutMessageWithLocation(MessageId, signedUserMessage.ContentType, intermediary);

        var signedReceiptResult = await NRRReceiptHashes(MessageId, signedUserMessage, adaptHashes);
        var messageStore = StubMessageStoreThatRetreives(signedUserMessage);

        // Act
        return await ExerciseVerifyNRRReceipt(messageStore, signedReceiptResult, verifyNrr);
    }

    private async Task<StepResult> ExerciseVerifyNRRReceipt(
        IAS4MessageBodyStore messageStore,
        AS4Message signedReceiptResult,
        bool verifyNrr)
    {
        var verifyNrrPMode = new SendingProcessingMode { ReceiptHandling = { VerifyNRR = verifyNrr }, Security = { SigningVerification = { AllowExpiredCertificate = true, AllowUnknownRootCertificate = true } } };
        var verifySignaturePMode = new ReceivingProcessingMode { Security = { SigningVerification = { Signature = Limit.Required, AllowExpiredCertificate = true, AllowUnknownRootCertificate = true } } };

        var step = new VerifySignatureAS4MessageStep(
            NullLogger<VerifySignatureAS4MessageStep>.Instance,
            Default.CertificateRepository,
            Default.NewOutMessageService(this, messageStore));

        return await step.ExecuteAsync(
            new MessagingContext(
                signedReceiptResult,
                MessagingContextMode.Receive)
            {
                SendingPMode = verifyNrrPMode,
                ReceivingPMode = verifySignaturePMode
            }, CancellationToken.None);
    }

    protected static async Task<AS4Message> NRRReceiptHashes(
        string messageId,
        AS4Message signedUserMessage,
        Func<byte[], byte[]> adaptHashes)
    {
        var references = signedUserMessage.SecurityHeader.GetReferences()
            .Select(r => new Reference(
                uri: r.Uri,
                transforms: [],
                digestMethod: new ReferenceDigestMethod(Constants.SignAlgorithms.Sha256),
                digestValue: adaptHashes(r.DigestValue ?? [])));

        var receipt = new Receipt(
            messageId: $"receipt-{Guid.NewGuid()}",
            refToMessageId: messageId,
            nonRepudiation: new NonRepudiationInformation(references));

        return await SerializeDeserializeSoap(
            AS4MessageUtils.SignWithCertificate(
                AS4Message.Create(receipt),
                new StubCertificateRepository().GetStubCertificate()));
    }

    protected void InsertOutMessageWithLocation(
        string messageId,
        string contentType,
        bool intermediary)
    {
        var repo = Default.NewDatastoreRepository(this);
        repo.InsertOutMessage(new OutMessage(messageId)
        {
            MessageLocation = messageId,
            ContentType = contentType,
            Intermediary = intermediary
        });
    }

    private static StubMessageBodyRetriever StubMessageStoreThatRetreives(AS4Message signedUserMessage)
    {
        return new StubMessageBodyRetriever(() =>
        {
            var serializer = Default.MimeMessageSerializer;
            var memory = new VirtualStream(VirtualStream.MemoryFlag.AutoOverFlowToDisk);
            serializer.Serialize(signedUserMessage, memory);
            memory.Position = 0;

            return memory;
        });
    }

    private static Task<AS4Message> SerializeDeserializeMime(AS4Message msg)
    {
        var serializer = Default.MimeMessageSerializer;
        var memory = new MemoryStream();
        serializer.Serialize(msg, memory);
        memory.Position = 0;

        return serializer.DeserializeAsync(memory, msg.ContentType, CancellationToken.None);
    }

    private static Task<AS4Message> SerializeDeserializeSoap(AS4Message msg)
    {
        var serializer = Default.SoapEnvelopeSerializer;
        var memory = new MemoryStream();
        serializer.Serialize(msg, memory);
        memory.Position = 0;

        return serializer.DeserializeAsync(memory, msg.ContentType, CancellationToken.None);
    }

    private static async Task<AS4Message> SignedUserMessage(string messageId)
    {
        var userMessage = AS4Message.Create(new UserMessage(messageId));
        userMessage.AddAttachment(new FilledAttachment());
        userMessage = await SerializeDeserializeMime(userMessage);

        return AS4MessageUtils.SignWithCertificate(userMessage, new StubCertificateRepository().GetStubCertificate());
    }

    protected static async Task<MessagingContext> DeserializeSignedMessage(string xml)
    {
        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var serializer = new SoapEnvelopeSerializer();

        const string ContentType =
            "multipart/related; boundary=\"=-dXYE+NJdacou7AbmYZgUPw==\"; type=\"application/soap+xml\"; charset=\"utf-8\"";

        var as4Message =
            await serializer.DeserializeAsync(memoryStream, ContentType, CancellationToken.None);

        return new MessagingContext(as4Message, MessagingContextMode.Unknown);
    }

    protected static ReceivingProcessingMode ReceivingPModeWithAllowedSigningVerification()
    {
        var receivingPMode = new ReceivingProcessingMode();
        receivingPMode.Security.SigningVerification.Signature = Limit.Allowed;
        receivingPMode.Security.SigningVerification.AllowExpiredCertificate = true;
        receivingPMode.Security.SigningVerification.AllowUnknownRootCertificate = true;

        return receivingPMode;
    }

    private async Task<StepResult> ExerciseVerify(MessagingContext ctx)
    {
        var sut = new VerifySignatureAS4MessageStep(
            NullLogger<VerifySignatureAS4MessageStep>.Instance,
            Default.CertificateRepository,
            Default.NewOutMessageService(this, Default.AS4MessageBodyFileStore));

        return await sut.ExecuteAsync(ctx, CancellationToken.None);
    }
}
