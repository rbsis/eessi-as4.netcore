using System.Xml;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Security.Strategies;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Logging;
using CollaborationInfo = Eu.EDelivery.AS4.Model.PMode.CollaborationInfo;
using MessageProperty = Eu.EDelivery.AS4.Model.PMode.MessageProperty;
using Party = Eu.EDelivery.AS4.Model.PMode.Party;
using PartyId = Eu.EDelivery.AS4.Model.PMode.PartyId;
using Service = Eu.EDelivery.AS4.Model.PMode.Service;

namespace Eu.EDelivery.AS4.Services.DynamicDiscovery;

/// <summary>
/// Dynamic Discovery profile that queries the local configuration to look for the right SMP info to complete the
/// <see cref="SendingProcessingMode" />.
/// </summary>
/// <seealso cref="IDynamicDiscoveryProfile" />
public class LocalDynamicDiscoveryProfile : IDynamicDiscoveryProfile
{
    private readonly ILogger<LocalDynamicDiscoveryProfile> _logger;
    private readonly IDatastoreRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDynamicDiscoveryProfile" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="repository"></param>
    public LocalDynamicDiscoveryProfile(ILogger<LocalDynamicDiscoveryProfile> logger, IDatastoreRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    /// <summary>
    /// Retrieves the SMP meta data <see cref="XmlDocument" /> for a given <paramref name="party" /> using a given
    /// <paramref name="properties" />.
    /// </summary>
    /// <param name="party">The party identifier to select the right SMP meta-data.</param>
    /// <param name="properties">The information properties specified in the <see cref="SendingProcessingMode"/> for this profile.</param>
    /// <param name="cancellation"></param>
    public Task<XmlDocument> RetrieveSmpMetaDataAsync(Model.Core.Party party, IDictionary<string, string> properties, CancellationToken cancellation)
    {
        if (party.PrimaryPartyId == null
            || party.PartyIds.FirstOrDefault()?.Type is null
            || party.Role == null)
        {
            throw new InvalidOperationException(
                "Given invalid 'ToParty', requires 'Role', 'PartyId', and 'PartyType'");
        }

        var configuration = _repository.FindSmpResponseForToParty(party);
        var xml = AS4XmlSerializer.ToString(configuration);

        var document = new XmlDocument();
        document.LoadXml(xml);

        return Task.FromResult(document);
    }

    /// <summary>
    /// Complete the <paramref name="pmode" /> with the SMP metadata that is present in the <paramref name="smpMetaData" />
    /// <see cref="XmlDocument" />
    /// </summary>
    /// <param name="pmode">The <see cref="SendingProcessingMode" /> that must be decorated with the SMP metadata</param>
    /// <param name="smpMetaData">An XmlDocument that contains the SMP MetaData that has been received from an SMP server.</param>
    /// <returns>The completed <see cref="SendingProcessingMode" /></returns>
    /// 
    public DynamicDiscoveryResult DecoratePModeWithSmpMetaData(SendingProcessingMode pmode, XmlDocument smpMetaData)
    {
        var smpResponse = AS4XmlSerializer.FromString<SmpConfiguration>(smpMetaData.OuterXml)
            ?? throw new ArgumentNullException(nameof(smpMetaData),
                $@"SMP Response cannot be deserialized correctly to a SmpConfiguration model: {smpMetaData.OuterXml}");

        OverridePushProtocolUrlWithTlsEnabling(pmode, smpResponse);
        OverrideEntireEncryption(pmode, smpResponse);
        OverrideToParty(pmode, smpResponse);
        OverrideCollaborationServiceAction(pmode, smpResponse);
        AddFinalRecipientToMessageProperties(pmode, smpResponse);

        return DynamicDiscoveryResult.Create(pmode);
    }

    private void OverridePushProtocolUrlWithTlsEnabling(SendingProcessingMode pmode, SmpConfiguration smpResponse)
    {
        _logger.LogDebug("Decorate SendingPMode {PModeId} with SMP from local store", pmode.Id);
        _logger.LogTrace("Override SendingPMode.PushConfiguration with {{Protocol.Url={Url}, TlsConfiguration.IsEnabled={TlsEnabled}}}",
            smpResponse.Url,
            smpResponse.TlsEnabled);

        pmode.PushConfiguration ??= new PushConfiguration();
        pmode.PushConfiguration.Protocol = pmode.PushConfiguration.Protocol ?? new Protocol();
        pmode.PushConfiguration.Protocol.Url = smpResponse.Url;

        pmode.PushConfiguration.TlsConfiguration = pmode.PushConfiguration.TlsConfiguration ?? new TlsConfiguration();
        pmode.PushConfiguration.TlsConfiguration.IsEnabled = smpResponse.TlsEnabled;
    }

    private void OverrideEntireEncryption(SendingProcessingMode pmode, SmpConfiguration smpResponse)
    {
        _logger.LogTrace("Override SendingPMode.Encryption with {{IsEnabled={EncryptionEnabled}}}", smpResponse.EncryptionEnabled);
        _logger.LogTrace("Override SendingPMode.Encryption with {{Algorithm={EncryptAlgorithm}, AlgorithmKeySize={EncryptAlgorithmKeySize}}}",
            smpResponse.EncryptAlgorithm,
            smpResponse.EncryptAlgorithmKeySize);

        _logger.LogTrace("Override SendingPMode.Encryption with {{CertificateType=PublicKeyCertificate}}");
        _logger.LogTrace("Override SendingPMode.Encryption.KeyTransport Algorithms with {{Digest={EncryptKeyDigestAlgorithm}, Mgf={EncryptKeyMgfAlorithm}, Transport={EncryptKeyTransportAlgorithm}}}",
            smpResponse.EncryptKeyDigestAlgorithm,
            smpResponse.EncryptKeyMgfAlorithm,
            smpResponse.EncryptKeyTransportAlgorithm);

        pmode.Security ??= new Model.PMode.Security();
        pmode.Security.Encryption = pmode.Security.Encryption ?? new Encryption();
        pmode.Security.Encryption.IsEnabled = smpResponse.EncryptionEnabled;
        pmode.Security.Encryption.Algorithm = smpResponse.EncryptAlgorithm ?? Constants.Namespaces.XmlEnc11Aes128;
        pmode.Security.Encryption.AlgorithmKeySize = smpResponse.EncryptAlgorithmKeySize;
        pmode.Security.Encryption.CertificateType = PublicKeyCertificateChoiceType.PublicKeyCertificate;
        if (smpResponse.EncryptPublicKeyCertificate != null)
        {
            pmode.Security.Encryption.EncryptionCertificateInformation = new PublicKeyCertificate
            {
                Certificate = TryConvertToBase64String(smpResponse.EncryptPublicKeyCertificate)
            };
        }
        pmode.Security.Encryption.KeyTransport = pmode.Security.Encryption.KeyTransport ?? new KeyEncryption();
        pmode.Security.Encryption.KeyTransport.DigestAlgorithm = smpResponse.EncryptKeyDigestAlgorithm ?? EncryptionStrategy.XmlEncSHA256Url;
        pmode.Security.Encryption.KeyTransport.MgfAlgorithm = smpResponse.EncryptKeyMgfAlorithm;
        pmode.Security.Encryption.KeyTransport.TransportAlgorithm = smpResponse.EncryptKeyTransportAlgorithm ?? EncryptionStrategy.XmlEncRSAOAEPUrlWithMgf;
    }

    private void OverrideToParty(SendingProcessingMode pmode, SmpConfiguration smpResponse)
    {
        _logger.LogTrace("Override SendingPMode.MessagingPackaging.ToParty with {{Role={PartyRole}, PartyId={ToPartyId}}}",
            smpResponse.PartyRole,
            smpResponse.ToPartyId);

        pmode.MessagePackaging ??= new SendMessagePackaging();
        pmode.MessagePackaging.PartyInfo = pmode.MessagePackaging.PartyInfo ?? new PartyInfo();
        pmode.MessagePackaging.PartyInfo.ToParty = new Party(
            smpResponse.PartyRole,
            new PartyId(smpResponse.ToPartyId) { Type = smpResponse.PartyType });
    }

    private string? TryConvertToBase64String(byte[] arr)
    {
        if (arr == null || !arr.Any())
        {
            return null;
        }

        try
        {
            return Convert.ToBase64String(arr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Convert to Base64 string failed");
            return null;
        }
    }

    private void OverrideCollaborationServiceAction(SendingProcessingMode pmode, SmpConfiguration smpResponse)
    {
        _logger.LogTrace("Override SendingPMode.MessagingPackaing.CollaborationInfo with {{ServiceType={ServiceType}, ServiceValue={ServiceValue}, Action={Action}}}",
            smpResponse.ServiceType,
            smpResponse.ServiceValue,
            smpResponse.Action);

        pmode.MessagePackaging ??= new SendMessagePackaging();
        pmode.MessagePackaging.CollaborationInfo = pmode.MessagePackaging.CollaborationInfo ?? new CollaborationInfo();
        pmode.MessagePackaging.CollaborationInfo.Action = smpResponse.Action;
        pmode.MessagePackaging.CollaborationInfo.Service = new Service
        {
            Type = smpResponse.ServiceType,
            Value = smpResponse.ServiceValue
        };
    }

    private static void AddFinalRecipientToMessageProperties(SendingProcessingMode pmode, SmpConfiguration smpResponse)
    {
        pmode.MessagePackaging ??= new SendMessagePackaging();
        pmode.MessagePackaging.MessageProperties = pmode.MessagePackaging.MessageProperties ?? [];

        if (smpResponse.FinalRecipient != null)
        {
            pmode.MessagePackaging.MessageProperties.Add(
                new MessageProperty
                {
                    Name = "finalRecipient",
                    Value = smpResponse.FinalRecipient
                });
        }
    }
}
