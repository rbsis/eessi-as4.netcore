using System.Xml;
using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Receivers;

public static class DefaultAgentReceiverRegistry
{
    private static readonly Dictionary<AgentType, Receiver> _receivers = [];

    static DefaultAgentReceiverRegistry()
    {
        _receivers.Add(
            AgentType.Forward,
            new Receiver
            {
                Type = typeof(DatastoreReceiver).AssemblyQualifiedName,
                Setting = CreateDatastoreSettings("OutMessages", Operation.ToBeForwarded, Operation.Forwarding)
            });

        _receivers.Add(
            AgentType.PushSend,
            new Receiver
            {
                Type = typeof(DatastoreReceiver).AssemblyQualifiedName,
                Setting = CreateDatastoreSettings("OutMessages", Operation.ToBeSent, Operation.Sending)
            });

        _receivers.Add(
            AgentType.OutboundProcessing,
            new Receiver
            {
                Type = typeof(DatastoreReceiver).AssemblyQualifiedName,
                Setting = CreateDatastoreSettings("OutMessages", Operation.ToBeProcessed, Operation.Processing)
            });

        _receivers.Add(
            AgentType.Deliver,
            new Receiver
            {
                Type = typeof(DatastoreReceiver).AssemblyQualifiedName,
                Setting = CreateDatastoreSettings("InMessages", Operation.ToBeDelivered, Operation.Delivering)
            });

        _receivers.Add(
            AgentType.Notify,
            new Receiver
            {
                Type = typeof(DatastoreReceiver).AssemblyQualifiedName,
                Setting = CreateDatastoreSettings("", Operation.ToBeNotified, Operation.Notifying)
            });
    }

    private static Setting[] CreateDatastoreSettings(string table, Operation filter, Operation update)
    {
        var fieldAttribute = new XmlDocument().CreateAttribute("Field");
        fieldAttribute.Value = "Operation";

        return
        [
            new Setting("Table", table),
            new Setting("Filter", filter.ToString()),
            new Setting("Update", update.ToString())
            {
                Attributes = [fieldAttribute]
            }
        ];
    }

    /// <summary>
    /// Gets the default <see cref="Receiver"/> for a requested <see cref="AgentType"/>.
    /// </summary>
    /// <param name="agentType"></param>
    /// <returns></returns>
    public static Receiver GetDefaultReceiverFor(AgentType agentType)
    {
        if (_receivers.TryGetValue(agentType, out var value))
        {
            return value;
        }

        return new Receiver { Type = typeof(InMemmoryReceiver).AssemblyQualifiedName };
    }
}
