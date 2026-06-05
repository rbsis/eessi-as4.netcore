using Eu.EDelivery.AS4.Fe.SmpConfiguration.Model;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
///     Interface for managing SMP configurations.
/// </summary>
public interface ISmpConfigurationService
{
    /// <summary>
    ///     Get all SMP configurations
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Collection containing all <see cref="SmpConfiguration" /></returns>
    Task<IEnumerable<SmpConfigurationRecord>> GetRecordsAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Get SMP configuration by identifier
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Matched <see cref="N:Eu.EDelivery.AS4.Fe.SmpConfiguration" /> if found
    /// </returns>
    Task<SmpConfigurationDetail> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    ///     Create an e new <see cref="SmpConfiguration" />
    /// </summary>
    /// <param name="detail">The SMP configuration.</param>
    /// <param name="cancellationToken"></param>
    Task<SmpConfigurationDetail> CreateAsync(SmpConfigurationDetail detail, CancellationToken cancellationToken);

    /// <summary>
    ///     Update an existing <see cref="SmpConfiguration" /> by id
    /// </summary>
    /// <param name="id">The id of the SmpConfiguration</param>
    /// <param name="detail">SMP configuration data to be updated</param>
    /// <param name="cancellationToken"></param>
    Task UpdateAsync(long id, SmpConfigurationDetail detail, CancellationToken cancellationToken);

    /// <summary>
    ///     Delete an existing <see cref="SmpConfiguration" /> by id
    /// </summary>
    /// <param name="id">The id of the <see cref="SmpConfiguration"/></param>
    /// <param name="cancellationToken"></param>
    Task DeleteAsync(long id, CancellationToken cancellationToken);
}
