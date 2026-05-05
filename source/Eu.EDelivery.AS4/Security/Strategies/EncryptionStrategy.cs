using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Security.Builders;
using Eu.EDelivery.AS4.Security.Encryption;
using MimeKit.IO;

namespace Eu.EDelivery.AS4.Security.Strategies;

/// <summary>
/// An <see cref="CryptoStrategy"/> implementation
/// responsible for the Encryption of the <see cref="AS4Message"/>
/// </summary>
internal class EncryptionStrategy : CryptoStrategy
{
    private readonly List<Attachment> _attachments;

    private readonly KeyEncryptionConfiguration _keyEncryptionConfig;
    private readonly DataEncryptionConfiguration _dataEncryptionConfig;


    private readonly List<EncryptedData> _encryptedDatas = [];

    private AS4EncryptedKey? _as4EncryptedKey;

    internal EncryptionStrategy(
        KeyEncryptionConfiguration keyEncryptionConfig,
        DataEncryptionConfiguration dataEncryptionConfig,
        IEnumerable<Attachment> attachments)
    {
        _keyEncryptionConfig = keyEncryptionConfig;
        _dataEncryptionConfig = dataEncryptionConfig;
        _attachments = [.. attachments];
    }

    /// <summary>
    /// Appends all encryption elements, such as <see cref="EncryptedKey"/> and <see cref="EncryptedData"/> elements.
    /// </summary>
    /// <param name="securityElement"></param>
    public void AppendEncryptionElements(XmlElement securityElement)
    {
        ArgumentNullException.ThrowIfNull(securityElement);

        if (securityElement.OwnerDocument == null)
        {
            throw new ArgumentException(@"SecurityHeader needs to have an OwnerDocument", nameof(securityElement));
        }

        var securityDocument = securityElement.OwnerDocument;

        // Add additional elements such as certificate references
        if (_as4EncryptedKey != null)
        {
            _as4EncryptedKey.SecurityTokenReference?.AppendSecurityTokenTo(securityElement, securityDocument);
            _as4EncryptedKey.AppendEncryptedKey(securityElement);
        }

        AppendEncryptedDataElements(securityElement, securityDocument);
    }

    private void AppendEncryptedDataElements(XmlElement securityElement, XmlDocument securityDocument)
    {
        foreach (var encryptedData in _encryptedDatas)
        {
            var encryptedDataElement = encryptedData.GetXml();
            var importedEncryptedDataNode = securityDocument.ImportNode(encryptedDataElement, deep: true);

            securityElement.AppendChild(importedEncryptedDataNode);
        }
    }

    /// <summary>
    /// Encrypts the <see cref="AS4Message"/> and its attachments.
    /// </summary>
    public void EncryptMessage()
    {
        _encryptedDatas.Clear();

        var encryptionKey = GenerateSymmetricKey(_dataEncryptionConfig.AlgorithmKeySize);
        var as4EncryptedKey = GetEncryptedKey(encryptionKey, _keyEncryptionConfig);

        _as4EncryptedKey = as4EncryptedKey;

        using var encryptionAlgorithm = CreateSymmetricAlgorithm(_dataEncryptionConfig.EncryptionMethod, encryptionKey);
        EncryptAttachmentsWithAlgorithm(as4EncryptedKey, encryptionAlgorithm);
    }

    private static byte[] GenerateSymmetricKey(int keySize)
    {
        using var aes = Aes.Create();
        aes.KeySize = keySize;
        aes.GenerateKey();

        return aes.Key;
    }

    private static AS4EncryptedKey GetEncryptedKey(
        byte[] symmetricKey,
        KeyEncryptionConfiguration keyEncryptionConfig)
    {
        return
            AS4EncryptedKey.CreateEncryptedKeyBuilderForKey(symmetricKey, keyEncryptionConfig)
                           .Build();
    }

    private void EncryptAttachmentsWithAlgorithm(
        AS4EncryptedKey encryptedKey,
        SymmetricAlgorithm encryptionAlgorithm)
    {
        foreach (var attachment in _attachments)
        {
            var encrypted = EncryptData(attachment.Content, encryptionAlgorithm);
            var encryptedData = CreateEncryptedDataForAttachment(attachment, encryptedKey);
            if (encryptedData.Id == null)
            {
                continue;
            }

            _encryptedDatas.Add(encryptedData);

            encryptedKey.AddDataReference(encryptedData.Id);
            attachment.UpdateContent(encrypted, "application/octet-stream");
        }
    }

    private EncryptedData CreateEncryptedDataForAttachment(Attachment attachment, AS4EncryptedKey encryptedKey) => new EncryptedDataBuilder()
        .WithDataEncryptionConfiguration(_dataEncryptionConfig)
        .WithMimeType(attachment.ContentType)
        .WithEncryptionKey(encryptedKey)
        .WithUri(attachment.Id)
        .Build();

    private Stream EncryptData(Stream secretStream, SymmetricAlgorithm algorithm)
    {
        Stream encryptedStream = CreateVirtualStreamOf(secretStream);

        var cryptoStream = new CryptoStream(encryptedStream, algorithm.CreateEncryptor(), CryptoStreamMode.Write);
        var origMode = algorithm.Mode;
        var origPadding = algorithm.Padding;

        try
        {
            algorithm.Mode = Mode;
            algorithm.Padding = Padding;
            secretStream.CopyTo(cryptoStream);
        }
        finally
        {
            cryptoStream.FlushFinalBlock();
            algorithm.Mode = origMode;
            algorithm.Padding = origPadding;
        }

        encryptedStream.Position = 0;

        if (Mode != CipherMode.ECB)
        {
            var chainedStream = new ChainedStream();
            chainedStream.Add(new MemoryStream(algorithm.IV));
            chainedStream.Add(encryptedStream);

            encryptedStream = chainedStream;
        }

        return encryptedStream;
    }
}
