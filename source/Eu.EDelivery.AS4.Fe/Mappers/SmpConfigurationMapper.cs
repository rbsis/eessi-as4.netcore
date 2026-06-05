using Eu.EDelivery.AS4.Fe.SmpConfiguration.Model;

namespace Eu.EDelivery.AS4.Fe.Mappers;

/// <summary>
///     Mapper for <seealso cref="Entities.SmpConfiguration" />
/// </summary>
public class SmpConfigurationMapper :
    IMapper<SmpConfigurationDetail, Entities.SmpConfiguration>,
    IMapper<Entities.SmpConfiguration, SmpConfigurationDetail>
{
    public Entities.SmpConfiguration Map(SmpConfigurationDetail source) => new()
    {
        Id = source.Id ?? 0,
        Action = source.Action,
        EncryptAlgorithm = source.EncryptAlgorithm,
        EncryptAlgorithmKeySize = source.EncryptAlgorithmKeySize,
        EncryptionEnabled = source.EncryptionEnabled,
        EncryptKeyDigestAlgorithm = source.EncryptKeyDigestAlgorithm,
        EncryptKeyMgfAlorithm = source.EncryptKeyMgfAlorithm,
        EncryptKeyTransportAlgorithm = source.EncryptKeyTransportAlgorithm,
        EncryptPublicKeyCertificateName = source.EncryptPublicKeyCertificateName,
        FinalRecipient = source.FinalRecipient,
        PartyRole = source.PartyRole,
        PartyType = source.PartyType,
        ServiceType = source.ServiceType,
        ServiceValue = source.ServiceValue,
        TlsEnabled = source.TlsEnabled,
        ToPartyId = source.ToPartyId,
        Url = source.Url,
    };

    public SmpConfigurationDetail Map(Entities.SmpConfiguration source) => new()
    {
        Id = source.Id,
        Action = source.Action,
        ServiceType = source.ServiceType,
        ServiceValue = source.ServiceValue,
        FinalRecipient = source.FinalRecipient,
        ToPartyId = source.ToPartyId,
        PartyRole = source.PartyRole,
        TlsEnabled = source.TlsEnabled,
        Url = source.Url,
        PartyType = source.PartyType,
        EncryptionEnabled = source.EncryptionEnabled,
        EncryptAlgorithm = source.EncryptAlgorithm,
        EncryptAlgorithmKeySize = source.EncryptAlgorithmKeySize,
        EncryptKeyDigestAlgorithm = source.EncryptKeyDigestAlgorithm,
        EncryptKeyMgfAlorithm = source.EncryptKeyMgfAlorithm,
        EncryptKeyTransportAlgorithm = source.EncryptKeyTransportAlgorithm,
        EncryptPublicKeyCertificate = source.EncryptPublicKeyCertificate == null
            ? null
            : Convert.ToBase64String(source.EncryptPublicKeyCertificate),
        EncryptPublicKeyCertificateName = source.EncryptPublicKeyCertificateName
    };
}
