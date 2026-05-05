using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Eu.EDelivery.AS4.Builders.Security;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Security.Encryption;
using Eu.EDelivery.AS4.Security.Signing;
using Eu.EDelivery.AS4.Security.Strategies;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Xml;
using MimeKit;

namespace Eu.EDelivery.AS4.Model.Core;

/// <summary>
/// Internal AS4 Message between MSH
/// </summary>
public sealed class AS4Message : IEquatable<AS4Message>
{
    private readonly bool _serializeAsMultiHop;
    private readonly List<Attachment> _attachmens;
    private readonly List<MessageUnit> _messageUnits;

    /// <summary>
    /// Prevents a default instance of the <see cref="AS4Message"/> class from being created.
    /// </summary>
    /// <param name="serializeAsMultiHop">if set to <c>true</c> [serialize as multi hop].</param>
    private AS4Message(bool serializeAsMultiHop = false)
    {
        _serializeAsMultiHop = serializeAsMultiHop;
        _attachmens = [];
        _messageUnits = [];

        ContentType = "application/soap+xml";
        SigningId = new SigningId();
        SecurityHeader = new SecurityHeader();
    }

    public static AS4Message Empty => new(serializeAsMultiHop: false);

    public string ContentType { get; private set; }

    public XmlDocument? EnvelopeDocument { get; set; }

    // ReSharper disable once InconsistentNaming
    private bool? __hasMultiHopAttribute;

    /// <summary>
    /// Gets a value indicating whether or not this AS4 Message is a MultiHop message.
    /// </summary>
    public bool IsMultiHopMessage => (__hasMultiHopAttribute ?? false) || (FirstSignalMessage?.IsMultihopSignal ?? false) || _serializeAsMultiHop;

    public IEnumerable<MessageUnit> MessageUnits => _messageUnits.AsReadOnly();

    public IEnumerable<UserMessage> UserMessages => MessageUnits.OfType<UserMessage>();

    public IEnumerable<SignalMessage> SignalMessages => MessageUnits.OfType<SignalMessage>();

    public IEnumerable<Attachment> Attachments => _attachmens.AsReadOnly();

    public SigningId SigningId { get; internal set; }

    public SecurityHeader SecurityHeader { get; internal set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S2365:Properties should not make collection or array copies", Justification = "<Pending>")]
    public string[] MessageIds
        => UserMessages.Select(m => m.MessageId).Concat(SignalMessages.Select(m => m.MessageId)).ToArray();

    public UserMessage? FirstUserMessage => UserMessages.FirstOrDefault();

    public SignalMessage? FirstSignalMessage => SignalMessages.FirstOrDefault();

    public bool IsSignalMessage => PrimaryMessageUnit is SignalMessage;

    public bool HasSignalMessage => MessageUnits.Any(m => m is SignalMessage);

    public bool IsUserMessage => PrimaryMessageUnit is UserMessage;

    public bool HasUserMessage => MessageUnits.Any(m => m is UserMessage);

    public MessageUnit? PrimaryMessageUnit => MessageUnits.FirstOrDefault();

    public bool IsSigned => SecurityHeader.IsSigned;

    public bool IsEncrypted => SecurityHeader.IsEncrypted;

    public bool HasAttachments => Attachments?.Any() ?? false;

    public bool IsEmpty => FirstSignalMessage is null && FirstUserMessage is null;

    public bool IsPullRequest => PrimaryMessageUnit is PullRequest;

    /// <summary>
    /// Creates message with a SOAP envelope.
    /// </summary>
    /// <param name="soapEnvelope">The SOAP envelope.</param>
    /// <param name="contentType">Type of the content.</param>
    /// <param name="securityHeader"></param>
    /// <param name="messagingHeader"></param>
    /// <param name="bodyElement"></param>
    /// <param name="cancellation"></param>
    ///<remarks>This method should only be used when creating an AS4 Message via deserialization.</remarks>
    /// <returns></returns>
    internal static async Task<AS4Message> CreateAsync(
        XmlDocument soapEnvelope,
        string contentType,
        SecurityHeader securityHeader,
        Messaging messagingHeader,
        Body05 bodyElement,
        CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var result = new AS4Message
        {
            EnvelopeDocument = soapEnvelope,
            ContentType = contentType,
            SecurityHeader = securityHeader
        };

        bool? IsMultihopAttributePresent()
        {
            const string MessagingXPath = "/*[local-name()='Envelope']/*[local-name()='Header']/*[local-name()='Messaging']";
            if (result.EnvelopeDocument?.SelectSingleNode(MessagingXPath) is XmlElement messagingNode)
            {
                var role = messagingNode.GetAttribute("role", Constants.Namespaces.Soap12);

                return !string.IsNullOrWhiteSpace(role) && role.Equals(Constants.Namespaces.EbmsNextMsh);
            }

            return null;
        }

        result.__hasMultiHopAttribute = IsMultihopAttributePresent();

        string? bodySecurityId = null;

        if (bodyElement.AnyAttr != null)
        {
            bodySecurityId = bodyElement.AnyAttr.FirstOrDefault(a => a.LocalName == "Id")?.Value;
        }

        result.SigningId = new SigningId(messagingHeader.SecurityId, bodySecurityId);

        result._messageUnits.AddRange(
            await SoapEnvelopeSerializer.GetMessageUnitsFromMessagingHeader(soapEnvelope, messagingHeader, cancellation));

        return result;
    }

    /// <summary>
    /// Creates message with a <see cref="SendingProcessingMode"/>.
    /// </summary>
    /// <param name="pmode">The pmode.</param>
    /// <returns></returns>
    public static AS4Message Create(SendingProcessingMode? pmode)
    {
        return new AS4Message(pmode?.MessagePackaging?.IsMultiHop == true);
    }

    /// <summary>
    /// Creates message with a <see cref="MessageUnit"/> and a optional <see cref="SendingProcessingMode"/>.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="pmode">The pmode.</param>
    /// <returns></returns>
    public static AS4Message Create(MessageUnit message, SendingProcessingMode? pmode = null)
    {
        var as4Message = Create(pmode);

        as4Message.AddMessageUnit(message);

        return as4Message;
    }

    /// <summary>
    /// Creates message with <see cref="MessageUnit"/>'s and a optional <see cref="SendingProcessingMode"/>.
    /// </summary>
    /// <param name="messages">The messages.</param>
    /// <param name="pmode">The pmode.</param>
    /// <returns></returns>
    public static AS4Message Create(IEnumerable<MessageUnit> messages, SendingProcessingMode? pmode = null)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Any(m => m is null))
        {
            throw new ArgumentNullException(nameof(messages), @"Message Units contains a 'null' reference");
        }

        var as4Message = Create(pmode);

        as4Message.AddMessageUnits(messages);

        return as4Message;
    }

    /// <summary>
    /// Gets the primary message identifier.
    /// </summary>
    /// <returns></returns>
    public string? GetPrimaryMessageId()
    {
        return IsUserMessage ? FirstUserMessage?.MessageId : FirstSignalMessage?.MessageId;
    }

    /// <summary>
    /// Adds a <see cref="MessageUnit"/> to the AS4 Message.
    /// </summary>
    /// <param name="messageUnit">The MessageUnit, which can be a signalmessage or a usermessage.</param>
    /// <remarks>Adding a MessageUnit will cause the EnvelopeDocument property to be set to null, since the 
    /// Envelope Document will no longer be in-sync.</remarks>
    public void AddMessageUnit(MessageUnit messageUnit)
    {
        _messageUnits.Add(messageUnit);
        EnvelopeDocument = null;
    }

    /// <summary>
    /// Updates a given <see cref="MessageUnit"/> in the <see cref="AS4Message"/>
    /// </summary>
    /// <param name="old"></param>
    /// <param name="replacement"></param>
    public void UpdateMessageUnit(MessageUnit old, MessageUnit replacement)
    {
        _messageUnits.Remove(old);
        _messageUnits.Add(replacement);
        EnvelopeDocument = null;
    }

    /// <summary>
    /// Adds <see cref="MessageUnit"/>'s to the AS4 Message.
    /// </summary>
    /// <param name="messageUnits">The MessageUnits, which can be a signalmessage or a usermessage.</param>
    /// <remarks>Adding a MessageUnit will cause the EnvelopeDocument property to be set to null, since the 
    /// Envelope Document will no longer be in-sync.</remarks>
    public void AddMessageUnits(IEnumerable<MessageUnit> messageUnits)
    {
        foreach (var messageUnit in messageUnits)
        {
            AddMessageUnit(messageUnit);
        }
    }

    /// <summary>
    /// Clears the MessageUnit collection.
    /// </summary>
    /// <remarks>Clearing the essageUnits will cause the EnvelopeDocument property to be set to null, since the 
    /// Envelope Document will no longer be in-sync.</remarks>
    public void ClearMessageUnits()
    {
        _messageUnits.Clear();
        EnvelopeDocument = null;
    }

    /// <summary>
    /// Add Attachments to <see cref="AS4Message" />
    /// </summary>
    /// <param name="attachments"></param>
    /// <exception cref="InvalidOperationException">Throws when there already exists an <see cref="Attachment"/> with the same id</exception>
    public void AddAttachments(IEnumerable<Attachment> attachments)
    {
        foreach (var a in attachments)
        {
            AddAttachment(a);
        }
    }

    /// <summary>
    /// Add Attachment to <see cref="AS4Message" />
    /// </summary>
    /// <param name="attachment"></param>
    /// <exception cref="InvalidOperationException">Throws when there already exists an <see cref="Attachment"/> with the same id</exception>
    public void AddAttachment(Attachment attachment)
    {
        if (!_attachmens.Contains(attachment))
        {
            _attachmens.Add(attachment);
            if (!ContentType.Contains(Constants.ContentTypes.Mime))
            {
                UpdateContentTypeHeader();
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot add attachment because there already exists an 'Attachment' with the Id={attachment.Id}");
        }
    }

    private void UpdateContentTypeHeader()
    {
        var contentTypeString = Constants.ContentTypes.Soap;
        if (Attachments.Any())
        {
            var contentType = new Multipart("related").ContentType;
            contentType.Parameters["type"] = contentTypeString;
            contentType.Charset = Encoding.UTF8.HeaderName.ToLowerInvariant();
            contentTypeString = contentType.ToString();
        }

        ContentType = contentTypeString.Replace("Content-Type: ", string.Empty);
    }

    /// <summary>
    /// Closes the attachments.
    /// </summary>
    public void CloseAttachments()
    {
        foreach (var attachment in Attachments)
        {
            attachment.Content.Dispose();
        }
    }

    /// <summary>
    /// Removes the given attachment from this message.
    /// </summary>
    /// <param name="tobeRemoved">The tobe removed.</param>
    public void RemoveAttachment(Attachment tobeRemoved)
    {
        var foundAttachment = _attachmens.FirstOrDefault(a => a == tobeRemoved);
        if (foundAttachment is not null)
        {
            _attachmens.Remove(foundAttachment);
            foundAttachment.Content?.Dispose();
        }

        if (!Attachments.Any())
        {
            ContentType = Constants.ContentTypes.Soap;
        }
    }

    /// <summary>
    /// Removes all the attachments present in this message.
    /// </summary>
    public void RemoveAllAttachments()
    {
        CloseAttachments();
        _attachmens.Clear();
        ContentType = Constants.ContentTypes.Soap;
    }

    /// <summary>
    /// Encrypts the AS4 Message using the specified <paramref name="keyEncryptionConfig"/>
    /// and <paramref name="dataEncryptionConfig"/>
    /// </summary>
    /// <param name="keyEncryptionConfig"></param>
    /// <param name="dataEncryptionConfig"></param>
    public void Encrypt(KeyEncryptionConfiguration keyEncryptionConfig, DataEncryptionConfiguration dataEncryptionConfig)
    {
        var encryptor = EncryptionStrategyBuilder
            .Create(this, keyEncryptionConfig)
            .WithDataEncryptionConfiguration(dataEncryptionConfig)
            .Build();

        SecurityHeader.Encrypt(encryptor);
    }

    /// <summary>
    /// Decrypt the AS4 Message using the specified <paramref name="certificate"/>.
    /// </summary>
    /// <param name="certificate"></param>
    public void Decrypt(X509Certificate2 certificate)
    {
        var decryptor = DecryptionStrategyBuilder
            .Create(this, certificate)
            .Build();

        SecurityHeader.Decrypt(decryptor);
    }

    /// <summary>
    /// Digitally signs the AS4Message using the given <paramref name="signatureConfiguration"/>
    /// </summary>
    /// <param name="signatureConfiguration"></param>
    public void Sign(CalculateSignatureConfig signatureConfiguration)
    {
        var signingStrategy = SignStrategy.ForAS4Message(this, signatureConfiguration);
        SecurityHeader.Sign(signingStrategy);
    }

    /// <summary>
    /// Verifies if the digital signature on the AS4 Message is valid.
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public bool VerifySignature(VerifySignatureConfig config)
    {
        if (EnvelopeDocument == null)
        {
            return false;
        }
        var verifier = new SignatureVerificationStrategy(EnvelopeDocument);
        return verifier.VerifySignature(config);
    }

    /// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
    /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(AS4Message? other)
    {
        if (other == null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (IsEmpty == other.IsEmpty)
        {
            return true;
        }

        return GetPrimaryMessageId() == other.GetPrimaryMessageId();
    }

    public override bool Equals(object? obj) => Equals(obj as AS4Message);

    public override int GetHashCode() => GetPrimaryMessageId()?.GetHashCode() ?? 0;
}
