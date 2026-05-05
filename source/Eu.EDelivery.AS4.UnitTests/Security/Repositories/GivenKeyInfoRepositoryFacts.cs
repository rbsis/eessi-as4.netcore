using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Eu.EDelivery.AS4.Security.References;
using Eu.EDelivery.AS4.Security.Repositories;

namespace Eu.EDelivery.AS4.UnitTests.Security.Repositories;

/// <summary>
/// Testing <see cref="KeyInfoRepository" />
/// </summary>
public class GivenKeyInfoRepositoryFacts
{
    public class GivenValidArguments : GivenKeyInfoRepositoryFacts
    {
        [Fact]
        public void ThenRepositoryGetsCertificateIfKeyInfoIsSecurityTokenReference()
        {
            // Arrange
#pragma warning disable SYSLIB0026 // Type or member is obsolete
            var expectedCertificate = new X509Certificate2();
#pragma warning restore SYSLIB0026 // Type or member is obsolete
            var keyInfo = CreateKeyInfoWithSecurityTokenReference(expectedCertificate);

            var sut = new KeyInfoRepository(keyInfo);

            // Act
            var actualCertificate = sut.GetCertificate();

            // Assert
            Assert.Equal(expectedCertificate, actualCertificate);
        }

        private static KeyInfo CreateKeyInfoWithSecurityTokenReference(X509Certificate2 expectedCertificate)
        {
            var binarySecurityTokenReference = new BinarySecurityTokenReference(expectedCertificate);

            var keyInfo = new KeyInfo();
            keyInfo.AddClause(binarySecurityTokenReference);

            return keyInfo;
        }

        [Fact]
        public void ThenRepositoryGetsCertificateIfKeyInfoHasEmbeddedCertificate()
        {
            // Arrange
#pragma warning disable SYSLIB0026 // Type or member is obsolete
            var expectedCertificate = new X509Certificate2();
#pragma warning restore SYSLIB0026 // Type or member is obsolete
            var keyInfo = CreateKeyInfoWithEmbeddedCertificate(expectedCertificate);

            var sut = new KeyInfoRepository(keyInfo);

            // Act
            var actualCertificate = sut.GetCertificate();

            // Assert
            Assert.Equal(expectedCertificate, actualCertificate);
        }

        private static KeyInfo CreateKeyInfoWithEmbeddedCertificate(X509Certificate expectedCertificate)
        {
            var keyInfoData = new KeyInfoX509Data(expectedCertificate);
            var keyInfo = new KeyInfo();
            keyInfo.AddClause(keyInfoData);

            return keyInfo;
        }
    }

    public class GivenInvalidArguments : GivenKeyInfoRepositoryFacts
    {
        [Fact]
        public void ThenRepositoryFailsToGetCertificateIfSecurityTokenReferenceHasntAnyCertificate()
        {
            // Arrange
            var keyInfo = new KeyInfo();
            var document = new XmlDocument();
            keyInfo.AddClause(new BinarySecurityTokenReference(document.CreateElement("dummy")));
            var repository = new KeyInfoRepository(keyInfo);

            // Act
            var actualCertificate = repository.GetCertificate();

            // Assert
            Assert.Null(actualCertificate);
        }

        [Fact]
        public void ThenRepositoryFailsToGetCertificateIfKeyInfoHasntAnyClauses()
        {
            // Arrange
            var sut = new KeyInfoRepository(new KeyInfo());

            // Act
            var actualCertificate = sut.GetCertificate();

            // Assert
            Assert.Null(actualCertificate);
        }
    }
}
