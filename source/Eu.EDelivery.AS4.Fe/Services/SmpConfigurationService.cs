using EnsureThat;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Mappers;
using Eu.EDelivery.AS4.Fe.SmpConfiguration.Model;
using Microsoft.EntityFrameworkCore;

namespace Eu.EDelivery.AS4.Fe.Services;

/// <summary>
///     Implementation of <see cref="ISmpConfigurationService" />
/// </summary>
/// <seealso cref="ISmpConfigurationService" />
public class SmpConfigurationService : ISmpConfigurationService
{
    private const string Base64CerHeader = "data:application/x-x509-ca-cert;base64,";
    private const string Base64PkcsHeader = "data:application/x-pkcs12;base64,";

    private readonly IDbContextFactory<DatastoreContext> _contextFactory;
    private readonly IMapper<SmpConfigurationDetail, Entities.SmpConfiguration> _entitiesMapper;
    private readonly IMapper<Entities.SmpConfiguration, SmpConfigurationDetail> _detailMapper;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SmpConfigurationService" /> class.
    /// </summary>
    /// <param name="contextFactory">The datastore context factory.</param>
    /// <param name="entitiesMapper">Instance of the mapper.</param>
    /// <param name="detailMapper">Instance of the mapper.</param>
    public SmpConfigurationService(
        IDbContextFactory<DatastoreContext> contextFactory,
        IMapper<SmpConfigurationDetail, Entities.SmpConfiguration> entitiesMapper,
        IMapper<Entities.SmpConfiguration, SmpConfigurationDetail> detailMapper)
    {
        _contextFactory = contextFactory;
        _entitiesMapper = entitiesMapper;
        _detailMapper = detailMapper;
    }

    /// <summary>
    ///     Get all SMP configurations
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Collection containing all <see cref="N:Eu.EDelivery.AS4.Fe.SmpConfiguration.Model.SmpConfigurationRecord" />
    /// </returns>
    public async Task<IEnumerable<SmpConfigurationRecord>> GetRecordsAsync(CancellationToken cancellationToken)
    {
        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var configurations = await datastoreContext.SmpConfigurations.ToListAsync(cancellationToken);

        return configurations.Select(smp => new SmpConfigurationRecord
        {
            Id = smp.Id,
            Action = smp.Action,
            Url = smp.Url,
            ServiceType = smp.ServiceType,
            ServiceValue = smp.ServiceValue,
            TlsEnabled = smp.TlsEnabled,
            ToPartyId = smp.ToPartyId,
            PartyRole = smp.PartyRole,
            EncryptionEnabled = smp.EncryptionEnabled,
            FinalRecipient = smp.FinalRecipient,
            PartyType = smp.PartyType
        });
    }

    /// <summary>
    ///     Get SMP configuration by identifier
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>
    ///     Matched <see cref="N:Eu.EDelivery.AS4.Fe.Model.SmpConfigurationDetail" /> if found
    /// </returns>
    public async Task<SmpConfigurationDetail> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await datastoreContext.SmpConfigurations.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException("No smp configuration found for the provided id.");

        return _detailMapper.Map(entity);
    }

    /// <summary>
    ///     Create an e new <see cref="N:Eu.EDelivery.AS4.Fe.Model.SmpConfigurationDetail" />
    /// </summary>
    /// <param name="detail">The SMP configuration.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<SmpConfigurationDetail> CreateAsync(SmpConfigurationDetail detail, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(detail, nameof(detail));

        ValidateSmpConfiguration(detail);

        var configuration = _entitiesMapper.Map(detail);
        configuration.EncryptPublicKeyCertificate = DeserializePublicKeyCertificate(detail.EncryptPublicKeyCertificate);

        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await datastoreContext.SmpConfigurations.AddAsync(configuration, cancellationToken);
        await datastoreContext.SaveChangesAsync(cancellationToken);

        return _detailMapper.Map(configuration);
    }

    /// <summary>
    ///     Update an existing <see cref="N:Eu.EDelivery.AS4.Fe.Model.SmpConfigurationDetail" /> by id
    /// </summary>
    /// <param name="id">The id of the SmpConfiguration</param>
    /// <param name="detail">SMP configuration data to be updated</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    public async Task UpdateAsync(long id, SmpConfigurationDetail detail, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(detail, nameof(detail));
        EnsureArg.IsTrue(id > 0, nameof(id));
        ValidateSmpConfiguration(detail);

        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await datastoreContext.SmpConfigurations.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"No smp configuration with id {id} found.");

        existing.Action = detail.Action;
        existing.EncryptAlgorithm = detail.EncryptAlgorithm;
        existing.EncryptAlgorithmKeySize = detail.EncryptAlgorithmKeySize;
        existing.EncryptionEnabled = detail.EncryptionEnabled;
        existing.EncryptKeyDigestAlgorithm = detail.EncryptKeyDigestAlgorithm;
        existing.EncryptKeyMgfAlorithm = detail.EncryptKeyMgfAlorithm;
        existing.EncryptKeyTransportAlgorithm = detail.EncryptKeyTransportAlgorithm;
        existing.EncryptPublicKeyCertificateName = detail.EncryptPublicKeyCertificateName;
        existing.FinalRecipient = detail.FinalRecipient;
        existing.PartyRole = detail.PartyRole;
        existing.PartyType = detail.PartyType;
        existing.ServiceType = detail.ServiceType;
        existing.ServiceValue = detail.ServiceValue;
        existing.TlsEnabled = detail.TlsEnabled;
        existing.ToPartyId = detail.ToPartyId;
        existing.Url = detail.Url;
        existing.EncryptPublicKeyCertificate = DeserializePublicKeyCertificate(detail.EncryptPublicKeyCertificate);

        datastoreContext.Entry(existing).State = EntityState.Modified;
        await datastoreContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     Delete an existing <see cref="N:Eu.EDelivery.AS4.Fe.Model.SmpConfigurationDetail" /> by id
    /// </summary>
    /// <param name="id">The id of the <see cref="N:Eu.EDelivery.AS4.Fe.Model.SmpConfigurationDetail" /></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        EnsureArg.IsTrue(id > 0, nameof(id));

        using var datastoreContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var smpConfiguration = await datastoreContext.SmpConfigurations.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"No smp configuration with id {id} found");

        datastoreContext.SmpConfigurations.Remove(smpConfiguration);

        await datastoreContext.SaveChangesAsync(cancellationToken);
    }

    private static byte[]? DeserializePublicKeyCertificate(string? base64)
    {
        if (!string.IsNullOrEmpty(base64))
        {
            if (base64.StartsWith(Base64CerHeader) || base64.StartsWith(Base64PkcsHeader))
            {
                // Convert the certificate string to a byte array
                var split = base64.Split(',');
                return Convert.FromBase64String(split[split.Length > 1 ? 1 : 0]);
            }

            return Convert.FromBase64String(base64);
        }

        return null;
    }

    private static void ValidateSmpConfiguration(SmpConfigurationDetail smpConfiguration)
    {
        EnsureArg.IsTrue(smpConfiguration.EncryptAlgorithmKeySize >= 0, nameof(smpConfiguration.EncryptAlgorithmKeySize));
        EnsureArg.IsNotNullOrWhiteSpace(smpConfiguration.PartyRole, nameof(smpConfiguration.PartyRole));
        EnsureArg.IsNotNullOrWhiteSpace(smpConfiguration.PartyType, nameof(smpConfiguration.PartyType));
        EnsureArg.IsNotNullOrWhiteSpace(smpConfiguration.ToPartyId, nameof(smpConfiguration.ToPartyId));
        EnsureArg.IsNotNullOrWhiteSpace(smpConfiguration.Url, nameof(smpConfiguration.Url));

        if (!string.IsNullOrEmpty(smpConfiguration.EncryptPublicKeyCertificate)
            && string.IsNullOrEmpty(smpConfiguration.EncryptPublicKeyCertificateName))
        {
            throw new BusinessException(
                "EncryptPublicKeyCertificateName needs to be provided when EncryptPublicKeyCertificate is not empty!");
        }
    }
}
