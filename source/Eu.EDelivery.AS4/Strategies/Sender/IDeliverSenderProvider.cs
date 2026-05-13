using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Strategies.Sender;

/// <summary>
/// Interface to define the <see cref="IDeliverSender" /> selection
/// </summary>
public interface IDeliverSenderProvider
{
    /// <summary>
    /// Get the right <see cref="IDeliverSender" /> implementation
    /// for a given <paramref name="deliverMethod" />
    /// </summary>
    /// <param name="deliverMethod"></param>
    /// <returns></returns>
    /// <exception cref="Exception">A delegate callback throws an exception.</exception>
    IDeliverSender GetDeliverSender(Method deliverMethod);
}
