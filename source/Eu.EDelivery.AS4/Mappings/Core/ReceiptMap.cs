using System.Xml;
using System.Xml.Serialization;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Xml;
using NonRepudiationInformation = Eu.EDelivery.AS4.Model.Core.NonRepudiationInformation;
using Receipt = Eu.EDelivery.AS4.Model.Core.Receipt;
using UserMessage = Eu.EDelivery.AS4.Model.Core.UserMessage;

namespace Eu.EDelivery.AS4.Mappings.Core;

internal static class ReceiptMap
{
    private static readonly XmlSerializer _nonRepudiationSerializer = new(typeof(Xml.NonRepudiationInformation));

    /// <summary>
    /// Maps from a domain model representation to a XML representation of an AS4 receipt.
    /// </summary>
    /// <param name="model">The domain model to convert.</param>
    internal static Xml.SignalMessage Convert(Receipt model) => new()
    {
        MessageInfo = new()
        {
            MessageId = model.MessageId,
            RefToMessageId = model.RefToMessageId,
            Timestamp = model.Timestamp.LocalDateTime
        },
        Receipt = new()
        {
            UserMessage = model.UserMessage is not null
                ? UserMessageMap.Convert(model.UserMessage)
                : null,
            NonRepudiationInformation = model.NonRepudiationInformation is not null
                ? MapNonRepudiationInformation(model.NonRepudiationInformation)
                : null
        }
    };

    /// <summary>
    /// Maps from a XML representation with an optional routing usermessage to a domain model representation of an AS4 receipt.
    /// </summary>
    /// <param name="xml">The XML representation to convert.</param>
    /// <param name="routingM">The optional routing usermessage element to include in the to be created receipt.</param>
    public static Receipt Convert(Xml.SignalMessage xml, Maybe<RoutingInputUserMessage> routingM)
    {
        ArgumentNullException.ThrowIfNull(xml.Receipt);
        ArgumentException.ThrowIfNullOrEmpty(xml.MessageInfo.MessageId);

        var messageId = xml.MessageInfo.MessageId;
        var refToMessageId = xml.MessageInfo?.RefToMessageId;
        var timestamp = xml.MessageInfo?.Timestamp.ToDateTimeOffset() ?? DateTimeOffset.Now;

        var nriM = GetNonRepudiationFromXml(xml.Receipt);
        var userM = GetUserMessageFromXml(xml.Receipt);

        var routingNriReceiptM =
            routingM.Zip(nriM, (routing, nri) => new Receipt(messageId, refToMessageId, timestamp, nri, routing));

        var routingUserReceiptM =
            routingM.Zip(userM, (routing, user) => new Receipt(messageId, refToMessageId, timestamp, user, routing));

        var routingReceipt =
            routingM.Select(routing => new Receipt(messageId, refToMessageId, timestamp, includedUserMessage: null, routedUserMessage: routing));

        var nriReceipt =
            nriM.Select(nri => new Receipt(messageId, refToMessageId, timestamp, nri, routedUserMessage: null));

        var userReceipt =
            userM.Select(user => new Receipt(messageId, refToMessageId, timestamp, user, routedUserMessage: null));

        return routingNriReceiptM
            .OrElse(routingUserReceiptM)
            .OrElse(routingReceipt)
            .OrElse(nriReceipt)
            .OrElse(userReceipt)
            .GetOrElse(() => new Receipt(messageId, refToMessageId, timestamp, includedUserMessage: null, routedUserMessage: null));
    }

    private static Maybe<NonRepudiationInformation> GetNonRepudiationFromXml(Xml.Receipt r)
    {
        var firstNrrElement = r.Any?.FirstOrDefault();

        if (firstNrrElement != null
            && firstNrrElement.LocalName.IndexOf("NonRepudiationInformation", StringComparison.OrdinalIgnoreCase) > -1)
        {
            var deserialize = _nonRepudiationSerializer.Deserialize(new XmlNodeReader(firstNrrElement));
            return Maybe.Just(MapNonRepudiationInformation((Xml.NonRepudiationInformation)deserialize!));
        }

        if (r.NonRepudiationInformation != null)
        {
            return Maybe.Just(MapNonRepudiationInformation(r.NonRepudiationInformation));
        }

        return Maybe<NonRepudiationInformation>.Nothing;
    }

    private static Xml.NonRepudiationInformation MapNonRepudiationInformation(NonRepudiationInformation model) => new()
    {
        MessagePartNRInformation = [.. model.MessagePartNRIReferences.Select(MapPartNRInformation)]
    };

    private static NonRepudiationInformation MapNonRepudiationInformation(Xml.NonRepudiationInformation xml)
    {
        if (!xml.MessagePartNRInformation.Any())
        {
            return new NonRepudiationInformation([]);
        }

        var references = xml.MessagePartNRInformation
            .Select(p => p.Item)
            .Where(i => i != null)
            .Cast<ReferenceType>()
            .Select(MapReference)
            .ToArray();

        return new NonRepudiationInformation(references);
    }

    private static MessagePartNRInformation MapPartNRInformation(Reference r) => new()
    {
        Item = new ReferenceType
        {
            URI = r.URI,
            DigestMethod = new DigestMethodType { Algorithm = r.DigestMethod.Algorithm },
            DigestValue = r.DigestValue,
            Transforms = [.. r.Transforms.Select(t => new TransformType { Algorithm = t.Algorithm })]
        }
    };

    private static Reference MapReference(ReferenceType r) => new(
        r.URI,
        r.Transforms?.Select(t => new ReferenceTransform(t.Algorithm)).ToArray() ?? [],
        new ReferenceDigestMethod(r.DigestMethod.Algorithm),
        r.DigestValue);

    private static Maybe<UserMessage> GetUserMessageFromXml(Xml.Receipt r) =>
        r.UserMessage == null ? Maybe.Nothing<UserMessage>() : Maybe.Just(UserMessageMap.Convert(r.UserMessage));
}
