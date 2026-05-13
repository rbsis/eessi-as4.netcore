using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Security.Encryption;
using Eu.EDelivery.AS4.Security.References;
using Eu.EDelivery.AS4.Security.Signing;
using Eu.EDelivery.AS4.Serialization;

namespace Eu.EDelivery.AS4.TestUtils;

public static class AS4MessageUtils
{
    public static AS4Message SignWithCertificate(AS4Message message, X509Certificate2 certificate)
    {
        var config = new CalculateSignatureConfig(certificate,
            X509ReferenceType.BSTReference,
            Constants.SignAlgorithms.Sha256,
            Constants.HashFunctions.Sha256);

        message.Sign(config);

        return message;
    }

    public static AS4Message EncryptWithCertificate(AS4Message message, X509Certificate2 certificate)
    {
        message.Encrypt(new KeyEncryptionConfiguration(certificate), DataEncryptionConfiguration.Default);

        return message;
    }

    public static async Task<AS4Message> SerializeDeserializeAsync(this ISerializerProvider serializerProvider, AS4Message message, CancellationToken cancellation)
    {
        var serializer = serializerProvider.Get(message.ContentType);

        using var targetStream = new MemoryStream();
        await serializer.SerializeAsync(message, targetStream, cancellation);

        targetStream.Position = 0;

        return await serializer.DeserializeAsync(targetStream, message.ContentType, cancellation);
    }

    public static void SaveToFile(this ISerializerProvider serializerProvider, AS4Message m, string path)
    {
        using var fs = File.Create(path);
        serializerProvider
            .Get(m.ContentType)
            .Serialize(m, fs);
    }
}
