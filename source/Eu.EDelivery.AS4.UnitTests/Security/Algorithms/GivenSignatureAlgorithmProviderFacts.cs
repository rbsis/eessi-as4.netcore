using System.Xml;
using Eu.EDelivery.AS4.Security.Algorithms;

namespace Eu.EDelivery.AS4.UnitTests.Security.Algorithms;

/// <summary>
/// Testing <see cref="SignatureAlgorithmProvider" />
/// </summary>
public class GivenSignatureAlgorithmProviderFacts
{
    public class GivenValidArguments : GivenSignatureAlgorithmProviderFacts
    {
        [Fact]
        public void ThenGetRsaSha256SignatureAlgorithmFromProviderSucceedsForNamespace()
        {
            // Arrange
            const string Key = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

            // Act
            var signatureAlgorithm = SignatureAlgorithmProvider.Get(Key);

            // Assert
            Assert.NotNull(signatureAlgorithm);
            Assert.IsType<RsaPkCs1Sha256SignatureAlgorithm>(signatureAlgorithm);
        }

        [Fact]
        public void ThenGetRsaSha256SignatureAlgorithmFromProviderSucceedsForXmlDocument()
        {
            // Arrange
            const string Key = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
            var xmlDocument = GetEnvelopeDocument(Key);

            // Act
            var signatureAlgorithm = SignatureAlgorithmProvider.Get(xmlDocument);

            // Assert
            Assert.NotNull(signatureAlgorithm);
            Assert.IsType<RsaPkCs1Sha256SignatureAlgorithm>(signatureAlgorithm);
        }

        [Fact]
        public void ThenGetRsaSha384SignatureAlgorithmFromProviderSucceedsForNamespace()
        {
            // Arrange
            const string Key = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha384";

            // Act
            var signatureAlgorithm = SignatureAlgorithmProvider.Get(Key);

            // Assert
            Assert.NotNull(signatureAlgorithm);
            Assert.IsType<RsaPkCs1Sha384SignatureDescription>(signatureAlgorithm);
        }

        [Fact]
        public void ThenGetRsaSha384SignatureAlgorithmFromProviderSucceedsForXmlDocument()
        {
            // Arrange
            const string Key = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha384";
            var xmlDocument = GetEnvelopeDocument(Key);

            // Act
            var signatureAlgorithm = SignatureAlgorithmProvider.Get(xmlDocument);

            // Assert
            Assert.NotNull(signatureAlgorithm);
            Assert.IsType<RsaPkCs1Sha384SignatureDescription>(signatureAlgorithm);
        }

        [Fact]
        public void ThenGetRsaSha512SignatureAlgorithmFromProviderSucceedsForNamespace()
        {
            // Arrange
            const string Key = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha512";

            // Act
            var signatureAlgorithm = SignatureAlgorithmProvider.Get(Key);

            // Assert
            Assert.NotNull(signatureAlgorithm);
            Assert.IsType<RsaPkCs1Sha512SignatureAlgorithm>(signatureAlgorithm);
        }

        [Fact]
        public void ThenGetRsaSha512SignatureAlgorithmFromProviderSucceedsForXmlDocument()
        {
            // Arrange
            const string Key = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha512";
            var xmlDocument = GetEnvelopeDocument(Key);

            // Act
            var signatureAlgorithm = SignatureAlgorithmProvider.Get(xmlDocument);

            // Assert
            Assert.NotNull(signatureAlgorithm);
            Assert.IsType<RsaPkCs1Sha512SignatureAlgorithm>(signatureAlgorithm);
        }
    }

    protected static XmlDocument GetEnvelopeDocument(string algorithm)
    {
        var xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><SignatureMethod Algorithm=\"{algorithm}\"/>");

        return xmlDocument;
    }
}
