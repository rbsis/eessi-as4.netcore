using System.ComponentModel;
using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Monitor.Model;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
/// Interface to implement a monitor service
/// </summary>
public interface IMonitorService
{
    /// <summary>
    /// Gets the exceptions.
    /// </summary>
    /// <param name="filter">Exception filter object</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">filter - Filter must be supplied
    /// or
    /// Direction - Direction cannot be null</exception>
    /// <exception cref="BusinessException">Could not get any exceptions, something went wrong.</exception>
    Task<MessageResult<ExceptionMessage>> GetExceptionsAsync(ExceptionFilter? filter, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the messages.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">
    /// filter - Filter cannot be null
    /// or
    /// Direction - Direction filter cannot be empty
    /// </exception>
    /// <exception cref="BusinessException">No messages found</exception>
    Task<MessageResult<Message>> GetMessagesAsync(MessageFilter? filter, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the related messages.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<MessageResult<Message>> GetRelatedMessagesAsync(Direction direction, string messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the pmode number.
    /// </summary>
    /// <param name="pmode">The pmode.</param>
    /// <returns></returns>
    string GetPmodeNumber(string pmode);

    /// <summary>
    /// Downloads the message body.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">messageId - messageId parameter cannot be null</exception>
    /// <exception cref="InvalidEnumArgumentException">direction</exception>
    Task<Stream> DownloadMessageBodyAsync(Direction direction, long id, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the exception body.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">messageId - messageId parameter cannot be null</exception>
    Task<Stream> DownloadExceptionMessageBodyAsync(Direction direction, long id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the exception detail.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string?> GetExceptionDetailAsync(Direction direction, long messageId, CancellationToken cancellationToken);
}
