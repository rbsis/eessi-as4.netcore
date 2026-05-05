using Eu.EDelivery.AS4.Model.Common;

namespace Eu.EDelivery.AS4.Model.Deliver;

/// <summary>
/// Describes all the fields that could be passed to the consuming business application when delivering messages. 
/// </summary>
public class DeliverMessage
{
    public MessageInfo MessageInfo { get; set; }
    public PartyInfo PartyInfo { get; set; }
    public CollaborationInfo CollaborationInfo { get; set; }
    public MessageProperty[] MessageProperties { get; set; }
    public Payload[] Payloads { get; set; }

    public DeliverMessage()
    {
        MessageInfo = new MessageInfo();
        PartyInfo = new PartyInfo();
        CollaborationInfo = new CollaborationInfo();
        MessageProperties = [];
        Payloads = [];
    }
}
