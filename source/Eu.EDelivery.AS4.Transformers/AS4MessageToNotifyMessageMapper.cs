using System.Xml;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Notify;

namespace Eu.EDelivery.AS4.Transformers;

internal static class AS4MessageToNotifyMessageMapper
{
    internal static NotifyMessage Convert(
        SignalMessage? tobeNotifiedSignal,
        Type receivedEntityType,
        XmlDocument soapEnvelope)
    {
        if (tobeNotifiedSignal is null)
        {
            throw new ArgumentNullException(
                nameof(tobeNotifiedSignal),
                @"No SignalMessage found to create a NotifyMessage from");
        }

        if (soapEnvelope == null)
        {
            throw new ArgumentNullException(
                nameof(soapEnvelope),
                @"No SOAP envelope document found to include in the NotifyMessage");
        }

        var status = GetStatus(tobeNotifiedSignal, receivedEntityType);

        var xpath = $"//eb:SignalMessage[eb:MessageInfo/eb:MessageId[text()='{tobeNotifiedSignal.MessageId}']]";
        var ns = new XmlNamespaceManager(soapEnvelope.NameTable);
        ns.AddNamespace("eb", Constants.Namespaces.EbmsXmlCore);

        var element = (XmlElement?)soapEnvelope.SelectSingleNode(xpath, ns)
            ?? throw new InvalidOperationException($"No element found at xpath: {xpath}");

        return new NotifyMessage
        {
            MessageInfo =
            {
                MessageId = tobeNotifiedSignal.MessageId,
                RefToMessageId = tobeNotifiedSignal.RefToMessageId
            },
            StatusInfo =
            {
                Status = status,
                Any = [element]
            }
        };
    }

    private static Status GetStatus(SignalMessage tobeNotifiedSignal, Type receivedEntityType)
    {
        if (typeof(ExceptionEntity).IsAssignableFrom(receivedEntityType)) return Status.Exception;
        if (tobeNotifiedSignal is Receipt) return Status.Delivered;
        return Status.Error;
    }
}
