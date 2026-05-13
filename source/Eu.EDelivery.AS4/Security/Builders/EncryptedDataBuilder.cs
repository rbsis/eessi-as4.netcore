using System.Security.Cryptography.Xml;
using Eu.EDelivery.AS4.Security.Encryption;
using Eu.EDelivery.AS4.Security.References;
using Eu.EDelivery.AS4.Security.Transforms;

namespace Eu.EDelivery.AS4.Security.Builders;

/// <summary>
/// Builder to create <see cref="EncryptedData"/> Models
/// </summary>
internal class EncryptedDataBuilder
{
    private DataEncryptionConfiguration? _data;
    private string? _mimeType;
    private string? _uri;
    private AS4EncryptedKey? _encryptionKey;


    public EncryptedDataBuilder WithEncryptionKey(AS4EncryptedKey encryptionKey)
    {
        _encryptionKey = encryptionKey;
        return this;
    }

    /// <summary>
    /// Add a <paramref name="uri"/> to the <see cref="EncryptedData"/>
    /// </summary>
    /// <param name="uri"></param>
    /// <returns></returns>
    public EncryptedDataBuilder WithUri(string uri)
    {
        _uri = uri;
        return this;
    }

    /// <summary>
    /// Add a <paramref name="mimeType"/> to the <see cref="EncryptedData"/>
    /// </summary>
    /// <param name="mimeType"></param>
    /// <returns></returns>
    public EncryptedDataBuilder WithMimeType(string mimeType)
    {
        _mimeType = mimeType;
        return this;
    }

    /// <summary>
    /// Add a <paramref name="data"/> to the <see cref="EncryptedData"/>
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public EncryptedDataBuilder WithDataEncryptionConfiguration(DataEncryptionConfiguration data)
    {
        _data = data;
        return this;
    }

    /// <summary>
    /// Build the <see cref="EncryptedData"/> Model
    /// </summary>
    /// <returns></returns>
    public EncryptedData Build()
    {
        var encryptedData = CreateEncryptedData();
        AssembleEncryptedData(encryptedData);

        return encryptedData;
    }

    private EncryptedData CreateEncryptedData()
    {
        return new EncryptedData
        {
            Id = "ed-" + Guid.NewGuid(),
            Type = _data?.EncryptionType,
            EncryptionMethod = _data != null ? new EncryptionMethod(_data.EncryptionMethod) : null,
            CipherData = new CipherData(),
            MimeType = _mimeType
        };
    }

    private void AssembleEncryptedData(EncryptedData encryptedData)
    {
        encryptedData.CipherData.CipherReference = new CipherReference("cid:" + _uri);
        encryptedData.CipherData.CipherReference.TransformChain.Add(new AttachmentCiphertextTransform());

        var referenceId = _encryptionKey?.GetReferenceId();
        if (referenceId != null)
        {
            encryptedData.KeyInfo.AddClause(new ReferenceSecurityTokenReference(referenceId));
        }
    }
}
