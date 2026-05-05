using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Security.References;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Send;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Model;
using Microsoft.Extensions.Logging.Abstractions;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send;

/// <summary>
/// Testing <see cref="SignAS4MessageStep" />
/// </summary>
public class GivenSigningAS4MessageStepFacts
{
    [Fact]
    public async Task DoesntSignIfMessageIsEmpty()
    {
        // Arrange
        var context = new MessagingContext(AS4Message.Empty, MessagingContextMode.Unknown);

        // Act
        var stepResult = await ExerciseSigning(context);

        // Assert
        AssertNotSignedSecurityHeader(stepResult);
    }

    [Fact]
    public async Task DoesntSignIfExistingSendPModeSigningIsDisabled()
    {
        // Arrange
        var context = AS4UserMessageWithAttachment();
        context.SendingPMode = PModeWithoutSigningSettings();

        // Act
        var stepResult = await ExerciseSigning(context);

        // Assert
        AssertNotSignedSecurityHeader(stepResult);
    }

    private static SendingProcessingMode PModeWithoutSigningSettings()
    {
        return new SendingProcessingMode { Security = { Signing = { IsEnabled = false } } };
    }

    private static void AssertNotSignedSecurityHeader(StepResult result)
    {
        var securityHeader = result.MessagingContext.AS4Message!.SecurityHeader;

        Assert.NotNull(securityHeader);
        Assert.False(securityHeader.IsSigned);
        Assert.False(securityHeader.IsEncrypted);
    }

    [Fact]
    public async Task FailsToSignMessageIfCertificateHasntRightKeySet()
    {
        // Arrange
        var certWithoutPrivateKey = new X509Certificate2(AccessPointA, access_point_a_password, X509KeyStorageFlags.Exportable);

        var pmode = PModeWithSigningSettings();
        pmode.Security.Signing.SigningCertificateInformation = new CertificateFindCriteria
        {
            CertificateFindValue = "AccessPointA"
        };

        var context = AS4UserMessageWithAttachment();
        context.SendingPMode = pmode;

        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(() => ExerciseSigning(context, certWithoutPrivateKey));
    }

    [Fact]
    public async Task SignUserMessageIfExistingPModeSigningIsEnabled()
    {
        // Arrange
        var context = AS4UserMessageWithAttachment();
        context.SendingPMode = PModeWithSigningSettings();

        // Act
        var result = await ExerciseSigning(context);

        // Assert
        Assert.NotNull(result.MessagingContext.AS4Message);
        Assert.True(result.MessagingContext.AS4Message.SecurityHeader.IsSigned);
    }

    [Fact]
    public async Task SignReceiptIfReceivingPModeResponseSigningIsEnabled()
    {
        // Arrange
        var ctx = new MessagingContext(
            AS4Message.Create(new Receipt($"receipt-{Guid.NewGuid()}", $"user-{Guid.NewGuid()}")),
            MessagingContextMode.Send)
        {
            ReceivingPMode = new ReceivingProcessingMode
            {
                ReplyHandling =
                {
                    ResponseSigning = CreateEnabledSigningInformation()
                }
            }
        };

        // Act
        var result = await ExerciseSigning(ctx);

        // Assert
        Assert.NotNull(result.MessagingContext.AS4Message);
        Assert.True(result.MessagingContext.AS4Message.IsSigned);
    }

    [Fact]
    public async Task FailToSignReceiptWhenReceivingPModeIsMissing()
    {
        // Arrange
        var ctx = new MessagingContext(
            AS4Message.Create(new Receipt($"receipt-{Guid.NewGuid()}", $"user-{Guid.NewGuid()}")),
            MessagingContextMode.Send);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExerciseSigning(ctx));
    }

    private static MessagingContext AS4UserMessageWithAttachment()
    {
        var as4Message = AS4Message.Create(new FilledUserMessage());
        as4Message.AddAttachment(new FilledAttachment());

        return new MessagingContext(as4Message, MessagingContextMode.Unknown);
    }

    private static SendingProcessingMode PModeWithSigningSettings()
    {
        return new SendingProcessingMode
        {
            Security =
            {
                Signing = CreateEnabledSigningInformation()
            }
        };
    }

    private static Signing CreateEnabledSigningInformation()
    {
        return new Signing
        {
            IsEnabled = true,
            KeyReferenceMethod = X509ReferenceType.BSTReference,
            SigningCertificateInformation = new CertificateFindCriteria
            {
                CertificateFindValue = "PartyA",
                CertificateFindType = X509FindType.FindBySubjectName
            },
            Algorithm = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
            HashFunction = "http://www.w3.org/2001/04/xmlenc#sha256"
        };
    }

    private static async Task<StepResult> ExerciseSigning(MessagingContext context)
    {
        return await ExerciseSigning(context, r => { });
    }

    private static async Task<StepResult> ExerciseSigning(MessagingContext context, X509Certificate2 certificate)
    {
        return await ExerciseSigning(context, r => r.CertificateStore.Add(certificate));
    }

    private static async Task<StepResult> ExerciseSigning(
        MessagingContext context,
        Action<StubCertificateRepository> updateStore)
    {
        var stubCertRepo = new StubCertificateRepository();
        updateStore(stubCertRepo);

        var sut = new SignAS4MessageStep(NullLogger<SignAS4MessageStep>.Instance, repository: stubCertRepo);
        return await sut.ExecuteAsync(context, CancellationToken.None);
    }
}
