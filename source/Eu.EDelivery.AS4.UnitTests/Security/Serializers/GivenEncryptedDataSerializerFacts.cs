using System.Security.Cryptography;
using System.Xml;
using Eu.EDelivery.AS4.Security.Serializers;
using Eu.EDelivery.AS4.Security.Transforms;

namespace Eu.EDelivery.AS4.UnitTests.Security.Serializers;

/// <summary>
/// Testing <see cref="EncryptedDataSerializer" />
/// </summary>
public class GivenEncryptedDataSerializerFacts
{
    public GivenEncryptedDataSerializerFacts()
    {
        CryptoConfig.AddAlgorithm(typeof(AttachmentCiphertextTransform), AttachmentCiphertextTransform.Url);
    }

    public class GivenValidArguments : GivenEncryptedDataSerializerFacts
    {
        [Fact]
        public void ThenSerializeEncryptedDatasSucceeds()
        {
            // Arrange
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(Properties.Resources.as4_encrypted_envelope);
            var serializer = new EncryptedDataSerializer(xmlDocument);

            // Act
            var encryptedDatas = serializer.SerializeEncryptedDatas();

            // Assert
            Assert.NotEmpty(encryptedDatas);
            Assert.Equal(2, encryptedDatas.Count());
        }
    }
}
