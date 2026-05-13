using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Mappings.Core;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Streaming;
using Eu.EDelivery.AS4.Xml;
using Error = Eu.EDelivery.AS4.Model.Core.Error;
using NotSupportedException = System.NotSupportedException;
using PullRequest = Eu.EDelivery.AS4.Model.Core.PullRequest;
using Receipt = Eu.EDelivery.AS4.Model.Core.Receipt;
using SignalMessage = Eu.EDelivery.AS4.Model.Core.SignalMessage;
using UserMessage = Eu.EDelivery.AS4.Model.Core.UserMessage;

namespace Eu.EDelivery.AS4.Serialization;

/// <summary>
/// Serialize <see cref="AS4Message" /> to a <see cref="Stream" />
/// </summary>
public partial class SoapEnvelopeSerializer : ISerializer
{
    private static readonly XmlWriterSettings _defaultXmlWriterSettings = new()
    {
        CloseOutput = false,
        Encoding = new UTF8Encoding(false)
    };

    /// <summary>
    /// Asynchronously serializes the given <see cref="AS4Message"/> to a given <paramref name="output"/> stream.
    /// </summary>
    /// <param name="message">The message to serialize.</param>
    /// <param name="output">The destination stream to where the message should be written.</param>
    /// <param name="cancellation">The token to control the cancellation of the serialization.</param>
    public Task SerializeAsync(
        AS4Message message,
        Stream output,
        CancellationToken cancellation = default)
    {
        return Task.Run(() => Serialize(message, output), cancellation);
    }

    /// <summary>
    /// Synchronously serializes the given <see cref="AS4Message"/> to a given <paramref name="output"/> stream.
    /// </summary>
    /// <param name="message">The message to serialize.</param>
    /// <param name="output">The destination stream to where the message should be written.</param>
    /// 
    public void Serialize(
        AS4Message message,
        Stream output)
    {
        var builder = new SoapEnvelopeBuilder(message.EnvelopeDocument);

        var securityHeader = GetSecurityHeader(message);
        if (securityHeader != null)
        {
            builder.SetSecurityHeader(securityHeader);
        }

        if (message.EnvelopeDocument == null)
        {
            SetMultiHopHeaders(builder, message);

            var messagingHeader = CreateMessagingHeader(message);

            builder.SetMessagingHeader(messagingHeader);
            builder.SetMessagingBody(message.SigningId.BodySecurityId);
        }

        using var writer = XmlWriter.Create(output, _defaultXmlWriterSettings);
        builder.Build().WriteTo(writer);
    }

    private static Messaging CreateMessagingHeader(AS4Message message)
    {
        static object ToGeneralMessageUnit(MessageUnit u) =>
        u switch
        {
            UserMessage um => UserMessageMap.Convert(um),
            Receipt r => ReceiptMap.Convert(r),
            Error e => ErrorMap.Convert(e),
            PullRequest pr => PullRequestMap.Convert(pr),
            _ => throw new NotSupportedException($"AS4Message contains unkown MessageUnit of type: {u.GetType()}"),
        };

        var messagingHeader = new Messaging
        {
            SecurityId = message.SigningId.HeaderSecurityId,
            MessageUnits = message.MessageUnits.Select(ToGeneralMessageUnit).ToArray()
        };

        if (message.IsMultiHopMessage)
        {
            messagingHeader.role = Constants.Namespaces.EbmsNextMsh;
            messagingHeader.mustUnderstand1 = true;
            messagingHeader.mustUnderstand1Specified = true;
        }

        return messagingHeader;
    }

    private static XmlNode? GetSecurityHeader(AS4Message message)
    {
        if (!message.IsSigned && !message.IsEncrypted)
        {
            return null;
        }

        return message.SecurityHeader?.GetXml();
    }

    private static void SetMultiHopHeaders(SoapEnvelopeBuilder builder, AS4Message as4Message)
    {
        if (as4Message.IsSignalMessage && as4Message.FirstSignalMessage!.IsMultihopSignal)
        {
            var to = new To { Role = Constants.Namespaces.EbmsNextMsh, PartyId = [] };
            builder.SetToHeader(to);

            var actionValue = as4Message.FirstSignalMessage.MultihopAction;
            builder.SetActionHeader(actionValue);

            var routingInput = new RoutingInput
            {
                UserMessage = as4Message.FirstSignalMessage.MultiHopRouting.UnsafeGet,
                mustUnderstand = false,
                mustUnderstandSpecified = true,
                IsReferenceParameter = true,
                IsReferenceParameterSpecified = true
            };

            builder.SetRoutingInput(routingInput);
        }
    }

    /// <summary>
    /// Asynchronously deserializes the given <paramref name="input"/> stream to an <see cref="AS4Message"/> model.
    /// </summary>
    /// <param name="input">The source stream from where the message should be read.</param>
    /// <param name="contentType">The content type required to correctly deserialize the message into different MIME parts.</param>
    /// <param name="cancellation">The token to control the cancellation of the deserialization.</param>
    public async Task<AS4Message> DeserializeAsync(
        Stream? input,
        string contentType,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        var envelopeDocument = new XmlDocument { PreserveWhitespace = true };
        envelopeDocument.Load(input);

        // Sometimes throws 'The 'http://www.w3.org/XML/1998/namespace:lang' attribute is not declared.'
        // ValidateEnvelopeDocument(envelopeDocument);

        var nsMgr = GetNamespaceManagerForDocument(envelopeDocument);

        var securityHeader = DeserializeSecurityHeader(envelopeDocument, nsMgr);
        var messagingHeader = DeserializeMessagingHeader(envelopeDocument, nsMgr)
            ?? throw new InvalidMessageException("The envelopeStream does not contain a Messaging element");
        var body = DeserializeBody(envelopeDocument, nsMgr);

        var as4Message = await AS4Message.CreateAsync(
            envelopeDocument,
            contentType,
            securityHeader,
            messagingHeader,
            body,
            cancellation);

        input.MovePositionToStreamStart();

        return as4Message;
    }

    private static XmlNamespaceManager GetNamespaceManagerForDocument(XmlDocument doc)
    {
        var nsMgr = new XmlNamespaceManager(doc.NameTable);

        nsMgr.AddNamespace("s", Constants.Namespaces.Soap12);
        nsMgr.AddNamespace("wsse", Constants.Namespaces.WssSecuritySecExt);
        nsMgr.AddNamespace("ds", Constants.Namespaces.XmlDsig);
        nsMgr.AddNamespace("xenc", Constants.Namespaces.XmlEnc);
        nsMgr.AddNamespace("eb3", Constants.Namespaces.EbmsXmlCore);

        return nsMgr;
    }

    private static SecurityHeader DeserializeSecurityHeader(XmlDocument envelopeDocument, XmlNamespaceManager nsMgr)
    {
        if (envelopeDocument.SelectSingleNode("/s:Envelope/s:Header/wsse:Security", nsMgr)
            is XmlElement securityHeader)
        {
            return new SecurityHeader(securityHeader);
        }

        return new SecurityHeader();

    }

    private static Messaging? DeserializeMessagingHeader(XmlDocument document, XmlNamespaceManager nsMgr)
    {
        var messagingHeader = document.SelectSingleNode("/s:Envelope/s:Header/eb3:Messaging", nsMgr);

        if (messagingHeader == null)
        {
            return null;
        }

        var s = new XmlSerializer(typeof(Messaging), SoapEnvelopeBuilder.MessagingAttributeOverrides);
        return s.Deserialize(new XmlNodeReader(messagingHeader)) as Messaging;
    }

    internal static async Task<IEnumerable<MessageUnit>> GetMessageUnitsFromMessagingHeader(
        XmlDocument envelopeDocument,
        Messaging messagingHeader,
        CancellationToken cancellation)
    {
        if (messagingHeader.MessageUnits == null)
        {
            return [];
        }

        var routing = await GetRoutingUserMessageFromXmlAsync(envelopeDocument, cancellation);
        MessageUnit ToMessageUnitModel(object u) =>
        u switch
        {
            Xml.UserMessage um => UserMessageMap.Convert(um),
            Xml.SignalMessage s => ConvertSignalMessageFromXml(s, routing),
            _ => throw new NotSupportedException($"AS4Message has unknown MessageUnit of type: {u.GetType()}"),
        };

        return messagingHeader.MessageUnits.Select(ToMessageUnitModel);
    }

    private static async Task<Maybe<RoutingInputUserMessage>> GetRoutingUserMessageFromXmlAsync(XmlDocument envelopeDocument, CancellationToken cancellation)
    {
        var routingInputTag = envelopeDocument.SelectSingleNode(@"//*[local-name()='RoutingInput']");
        if (routingInputTag != null)
        {
            var routingInput = await AS4XmlSerializer.FromStringAsync<RoutingInput>(routingInputTag.OuterXml, cancellation);
            if (routingInput?.UserMessage != null)
            {
                return Maybe.Just(routingInput.UserMessage);
            }
        }

        return Maybe<RoutingInputUserMessage>.Nothing;
    }

    private static SignalMessage ConvertSignalMessageFromXml(Xml.SignalMessage signalMessage, Maybe<RoutingInputUserMessage> routing)
    {
        if (signalMessage.Error != null)
        {
            return ErrorMap.Convert(signalMessage, routing);
        }

        if (signalMessage.PullRequest != null)
        {
            return PullRequestMap.Convert(signalMessage);
        }

        if (signalMessage.Receipt != null)
        {
            return ReceiptMap.Convert(signalMessage, routing);
        }

        throw new NotSupportedException("Unable to map Xml.SignalMessage to SignalMessage");
    }

    // ReSharper disable once InconsistentNaming - only used here.
    private static readonly XmlSerializer _bodySerializer = new(typeof(Body05));

    private static Body05 DeserializeBody(XmlDocument envelopeDocument, XmlNamespaceManager nsMgr)
    {
        var bodyElement = envelopeDocument.SelectSingleNode("/s:Envelope/s:Body", nsMgr)
            ?? throw new InvalidMessageException("Body not found");

        return _bodySerializer.Deserialize(new XmlNodeReader(bodyElement)) as Body05
            ?? throw new InvalidMessageException("Body not deserialized");
    }
}
