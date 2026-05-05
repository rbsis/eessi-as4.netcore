using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Strategies.Sender;

/// <summary>
/// Interface to define the <see cref="INotifySender" /> selection
/// </summary>
public interface INotifySenderProvider
{
    /// <summary>
    /// Get the right <see cref="INotifySender" /> implementation
    /// for a given <paramref name="notifyMethod" />
    /// </summary>
    /// <param name="notifyMethod"></param>
    /// <returns></returns>
    /// <exception cref="Exception">A delegate callback throws an exception.</exception>
    INotifySender GetNotifySender(Method notifyMethod);
}
