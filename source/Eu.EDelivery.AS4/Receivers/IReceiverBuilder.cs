using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Receivers;

public interface IReceiverBuilder
{
    IReceiver BuildFromConfig(Receiver config);
}
