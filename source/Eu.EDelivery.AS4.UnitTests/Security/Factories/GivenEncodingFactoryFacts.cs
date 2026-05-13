using System.Reflection;
using System.Xml;
using Eu.EDelivery.AS4.Security.Encryption;
using Eu.EDelivery.AS4.Security.Factories;
using Eu.EDelivery.AS4.Security.Strategies;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Encodings;

namespace Eu.EDelivery.AS4.UnitTests.Security.Factories;

/// <summary>
/// Testing <see cref="EncodingFactory" />
/// </summary>
public class GivenEncodingFactoryFacts
{
    public class GivenValidArguments : GivenEncodingFactoryFacts
    {
        [Theory]
        [InlineData("http://www.w3.org/2001/04/xmlenc#sha256")]
        [InlineData(EncryptionStrategy.XmlEncSHA1Url)]
        public void ThenCreateEncodingWithDefaultsSucceeds(string algorithmName)
        {
            // Act
            var encoding = EncodingFactory.Create(algorithmName);

            // Assert
            AssertEqualRSAOAPPadding(encoding.AlgorithmName);
            AssertMgf1Hash(encoding, "SHA-1");
        }

        [Fact]
        public void ThenCreateEncodingWithGivenMgfSucceeds()
        {
            // Arrange
            const string MgfAlgorithmName = "http://www.w3.org/2009/xmlenc11#mgf1sha256";

            // Act
            var encoding = EncodingFactory.Create(null, MgfAlgorithmName);

            // Assert
            AssertEqualRSAOAPPadding(encoding.AlgorithmName);
            AssertMgf1Hash(encoding, "SHA-256");
        }

        private static void AssertEqualRSAOAPPadding(string algorithmName)
        {
            Assert.Equal("RSA/OAEPPadding", algorithmName);
        }
    }

    public class GivenValidEncryptedKeyXmlDocument : GivenEncodingFactoryFacts
    {
        [Fact]
        public void ThenCreateCorrectEncoding()
        {
            // Arrange
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(Properties.Resources.EncryptedKeyWithMGFSpec);
            var as4EncryptedKey = AS4EncryptedKey.LoadFromXmlDocument(xmlDocument);

            // Act
            var encoding = EncodingFactory.Create(
                as4EncryptedKey.GetDigestAlgorithm(),
                as4EncryptedKey.GetMaskGenerationFunction());

            // Assert
            AssertMgf1Hash(encoding, "SHA-256");
        }
    }

    protected static void AssertMgf1Hash(OaepEncoding encoding, string expectedValue)
    {
        var mgf1HashProperty = typeof(OaepEncoding).GetField(
            "mgf1Hash",
            BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.True(mgf1HashProperty != null, "Field mgf1Hash could not be found in OaepEncoding type.");

        var digest = mgf1HashProperty.GetValue(encoding) as IDigest;
        Assert.NotNull(digest);
        Assert.True(StringComparer.OrdinalIgnoreCase.Equals(expectedValue, digest.AlgorithmName));
    }
}
