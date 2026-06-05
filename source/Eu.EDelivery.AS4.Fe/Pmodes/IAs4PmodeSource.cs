using Eu.EDelivery.AS4.Fe.Modules;
using Eu.EDelivery.AS4.Fe.Pmodes.Model;

namespace Eu.EDelivery.AS4.Fe.Pmodes;

/// <summary>
/// As4 PMode source
/// </summary>
/// <seealso cref="IModular" />
public interface IAs4PmodeSource : IModular
{
    /// <summary>
    /// Gets the receiving names.
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<string>> GetReceivingNamesAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Gets the name of the receiving by.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ReceivingBasePmode?> GetReceivingByNameAsync(string name, CancellationToken cancellationToken);
    /// <summary>
    /// Gets the sending names.
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<string>> GetSendingNamesAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Gets the name of the sending by.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<SendingBasePmode?> GetSendingByNameAsync(string name, CancellationToken cancellationToken);
    /// <summary>
    /// Creates the receiving.
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CreateReceivingAsync(ReceivingBasePmode basePmode, CancellationToken cancellationToken);
    /// <summary>
    /// Deletes the receiving.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteReceivingAsync(string name, CancellationToken cancellationToken);
    /// <summary>
    /// Deletes the sending.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteSendingAsync(string name, CancellationToken cancellationToken);
    /// <summary>
    /// Creates the sending.
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CreateSendingAsync(SendingBasePmode basePmode, CancellationToken cancellationToken);
    /// <summary>
    /// Updates the sending.
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task UpdateSendingAsync(SendingBasePmode basePmode, string originalName, CancellationToken cancellationToken);
    /// <summary>
    /// Updates the receiving.
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task UpdateReceivingAsync(ReceivingBasePmode basePmode, string originalName, CancellationToken cancellationToken);
    /// <summary>
    /// Gets the pmode number.
    /// </summary>
    /// <param name="pmodeString">The pmode string.</param>
    /// <returns></returns>
    string? GetPmodeNumber(string pmodeString);
}
