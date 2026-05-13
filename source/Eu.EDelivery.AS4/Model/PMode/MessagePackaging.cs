using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Eu.EDelivery.AS4.Model.PMode;

public class MessagePackaging
{
    public PartyInfo? PartyInfo { get; set; }

    public CollaborationInfo? CollaborationInfo { get; set; }

    [XmlArray("MessageProperties")]
    [XmlArrayItem("MessageProperty")]
    public List<MessageProperty>? MessageProperties { get; set; }
}

public class PartyInfo
{
    public Party? FromParty { get; set; }
    public Party? ToParty { get; set; }

    #region Serialization Control properties

    [XmlIgnore]
    [JsonIgnore]
    public bool FromPartySpecified
    {
        get
        {
            var hasRole = !string.IsNullOrEmpty(FromParty?.Role);
            var hasPartyId = FromParty?.PartyIds?.All(p => !string.IsNullOrEmpty(p.Id)) ?? false;

            return hasRole && hasPartyId;
        }
    }

    [XmlIgnore]
    [JsonIgnore]
    public bool ToPartySpecified
    {
        get
        {
            var hasRole = !string.IsNullOrEmpty(ToParty?.Role);
            var hasPartyId = ToParty?.PartyIds?.All(p => !string.IsNullOrEmpty(p.Id)) ?? false;

            return hasRole && hasPartyId;
        }
    }

    #endregion
}
