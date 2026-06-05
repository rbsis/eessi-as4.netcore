using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Modules;
using Eu.EDelivery.AS4.Fe.Pmodes.Model;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
/// Interface to implement a pmode service
/// </summary>
/// <seealso cref="IModular" />
public interface IPmodeService : IModular
{
    /// <summary>
    /// Gets the receiving names.
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<string>> GetReceivingNamesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get a list of receiving pmodes
    /// </summary>
    /// <param name="name">The name of the pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ReceivingBasePmode?> GetReceivingByNameAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Get a list of sending pmodes
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<string>> GetSendingNamesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get a sending pmode by name
    /// </summary>
    /// <param name="name">The name of the pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<SendingBasePmode?> GetSendingByNameAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Create a receiving pmode
    /// </summary>
    /// <param name="basePmode">The pmode to create</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">Exception thrown when a pmode with the supplied name already exists</exception>
    Task CreateReceivingAsync(ReceivingBasePmode basePmode, CancellationToken cancellationToken);

    /// <summary>
    /// Create sending pmode
    /// </summary>
    /// <param name="basePmode">The pmode to create.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">Exception thrown when a pmode with the supplied name already exists</exception>
    Task CreateSendingAsync(SendingBasePmode basePmode, CancellationToken cancellationToken);

    /// <summary>
    /// Delete a receiving pmode
    /// </summary>
    /// <param name="name">The name of the pmode to delete.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException">Exception thrown when the pmode doesn't exist</exception>
    Task DeleteReceivingAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Delete a sending pmode
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException">Exception thrown when the pmode doesn't exist</exception>
    Task DeleteSendingAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Update sending pmode
    /// </summary>
    /// <param name="basePmode">Date to update the sending pmode with</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">Exception thrown when a sending pmode with the supplied name already exists</exception>
    Task UpdateSendingAsync(SendingBasePmode basePmode, string originalName, CancellationToken cancellationToken);

    /// <summary>
    /// Update receiving pmode
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="AlreadyExistsException">Exception thrown when a pmode with the supplied name already exists.</exception>
    Task UpdateReceivingAsync(ReceivingBasePmode basePmode, string originalName, CancellationToken cancellationToken);
}
