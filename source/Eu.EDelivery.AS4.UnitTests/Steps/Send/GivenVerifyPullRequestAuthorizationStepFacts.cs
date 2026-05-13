using System.Security;
using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services.PullRequestAuthorization;
using Eu.EDelivery.AS4.Steps.Send;
using Eu.EDelivery.AS4.TestUtils;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Services;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send;

public class GivenVerifyPullRequestAuthorizationStepFacts
{
    [Fact]
    public async Task ContinuesExecutionIfMatchedCertificateCanBeFoundForTheMpc()
    {
        // Arrange
        var signingCertificate = GetSigningCertificate();

        const string ExpectedMpc = "message-mpc";
        var context = ContextWithSignedPullRequest(ExpectedMpc, signingCertificate);

        var stubMap = new StubAuthorizationMapProvider([new PullRequestAuthorizationEntry(ExpectedMpc, signingCertificate.Thumbprint, true)]);
        var service = new PullAuthorizationMapService(stubMap, new StubCertificateRepository());
        var sut = new VerifyPullRequestAuthorizationStep(service);

        // Act
        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.CanProceed);
    }

    [Fact]
    public async Task FailsToAuthorizeWhenNoCertificateMatchesMpc()
    {
        // Arrange
        var signingCertificate = GetSigningCertificate();

        const string ExpectedMpc = "message-mpc";
        var context = ContextWithSignedPullRequest(ExpectedMpc, signingCertificate);

        var stubMap = new StubAuthorizationMapProvider([new PullRequestAuthorizationEntry(ExpectedMpc, "ANOTHERTHUMBPRINT", true)]);
        var service = new PullAuthorizationMapService(stubMap, new StubCertificateRepository());
        var sut = new VerifyPullRequestAuthorizationStep(service);

        // Act and assert.
        await Assert.ThrowsAsync<SecurityException>(() => sut.ExecuteAsync(context, CancellationToken.None));
    }

    private static X509Certificate2 GetSigningCertificate()
    {
        var cert = new X509Certificate2(Properties.Resources.holodeck_partya_certificate,
                                       Properties.Resources.certificate_password, X509KeyStorageFlags.Exportable);

        Assert.NotNull(cert.GetRSAPrivateKey());

        return cert;
    }

    private static MessagingContext ContextWithSignedPullRequest(string expectedMpc, X509Certificate2 signingCertificate)
    {
        var message = AS4Message.Create(new PullRequest($"pr-{Guid.NewGuid()}", expectedMpc));

        message = AS4MessageUtils.SignWithCertificate(message, signingCertificate);

        return new MessagingContext(message, MessagingContextMode.Send);
    }
}
