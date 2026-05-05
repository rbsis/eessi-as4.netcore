using System.Text;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Serialization;

namespace Eu.EDelivery.AS4.Model.Deliver;

public class DeliverMessageEnvelope
{
    private byte[] _alreadySerializedDeliverMessage;

    public string ContentType { get; }

    public IEnumerable<Attachment> Attachments { get; }

    public DeliverMessage Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliverMessageEnvelope"/> class.
    /// </summary>
    public DeliverMessageEnvelope(
        DeliverMessage message,
        string contentType,
        IEnumerable<Attachment> attachments)
    {
        _alreadySerializedDeliverMessage = [];

        Message = message;
        ContentType = contentType;
        Attachments = attachments;
    }

    internal DeliverMessageEnvelope(
        MessageInfo messageInfo,
        byte[] deliverMessage,
        string contentType) : this(new DeliverMessage { MessageInfo = messageInfo }, deliverMessage, contentType, Enumerable.Empty<Attachment>()) { }

    internal DeliverMessageEnvelope(
        DeliverMessage message,
        byte[] deliverMessage,
        string contentType,
        IEnumerable<Attachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(message);

        ArgumentNullException.ThrowIfNull(deliverMessage);

        ArgumentNullException.ThrowIfNull(contentType);

        ArgumentNullException.ThrowIfNull(attachments);

        _alreadySerializedDeliverMessage = deliverMessage;

        Message = message;
        ContentType = contentType;
        Attachments = attachments;
    }

    public byte[] SerializeMessage()
    {
        if (_alreadySerializedDeliverMessage.Length == 0)
        {
            _alreadySerializedDeliverMessage = Encoding.UTF8.GetBytes(AS4XmlSerializer.ToString(Message));
        }

        return _alreadySerializedDeliverMessage;
    }
}
