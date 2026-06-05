namespace Eu.EDelivery.AS4.Fe.Monitor;

/// <summary>
/// Interface to be implemented to send Submit tool messages to the client
/// </summary>
public interface IClient
{
    /// <summary>
    /// Send info log
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendInfoAsync(string message, CancellationToken cancellationToken);

    /// <summary>
    /// Send log containing PMode
    /// </summary>
    /// <param name="pmode">The pmode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendPmodeAsync(string pmode, CancellationToken cancellationToken);

    /// <summary>
    /// Sends log containing error
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendErrorAsync(string message, CancellationToken cancellationToken);

    /// <summary>
    /// Sendg log containing an AS4 message
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="id">The id of the AS4 message</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendAs4MessageAsync(string message, string id, CancellationToken cancellationToken);
}
