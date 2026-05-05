using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;
using System.Xml;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.PMode;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Services.DynamicDiscovery;

/// <summary>
/// Dynamic Discovery profile to retrieve a compliant eDelivery SMP profile based on the OpenPEPPOL BIS/CEN BII Service Metadata Publishers (SMP)
/// to extract information about the unknown receiver MSH. After a successful retrieval, the <see cref="SendingProcessingMode"/> can be extended
/// with the endpoint address, service value/type, action, receiver party and the public encryption certificate of the receiving MSH.
/// </summary>
public class PeppolDynamicDiscoveryProfile : IDynamicDiscoveryProfile
{
    private const string DocumentIdentifier = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:www.cenbii.eu:transaction:biitrns010:ver2.0:extended:urn:www.peppol.eu:bis:peppol5a:ver2.0::2.1";
    private const string DocumentIdentifierScheme = "busdox-docid-qns";

    private readonly ILogger<PeppolDynamicDiscoveryProfile> _logger;
    private static readonly HttpClient _httpClient = new();

    public PeppolDynamicDiscoveryProfile(ILogger<PeppolDynamicDiscoveryProfile> logger)
    {
        _logger = logger;
        SmlScheme = "iso6523-actorid-upis";
        SmpServerDomainName = "isaitb.acc.edelivery.tech.ec.europa.eu";
    }

    [Info("SML Scheme", defaultValue: "iso6523-actorid-upis")]
    [Description("Used to build the SML Uri")]
    // Property is used to determine the configuration options via reflection
    // ReSharper disable once UnusedMember.Local
    // ReSharper disable once UnassignedGetOnlyAutoProperty
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private string SmlScheme { get; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [Info("SMP Server Domain Name", defaultValue: "isaitb.acc.edelivery.tech.ec.europa.eu")]
    [Description("Domain name that must be used in the Uri")]
    // Property is used to determine the configuration options via reflection
    // ReSharper disable once UnusedMember.Local
    // ReSharper disable once UnassignedGetOnlyAutoProperty
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private string SmpServerDomainName { get; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private sealed class ESensConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ESensConfig"/> class.
        /// </summary>
        /// <param name="smlScheme">The SML scheme.</param>
        /// <param name="smpServerDomainName">Name of the SMP server domain.</param>
        private ESensConfig(string smlScheme, string smpServerDomainName)
        {
            ArgumentNullException.ThrowIfNull(smlScheme);

            ArgumentNullException.ThrowIfNull(smpServerDomainName);

            SmlScheme = smlScheme;
            SmpServerDomainName = smpServerDomainName;
        }

        public string SmlScheme { get; }

        public string SmpServerDomainName { get; }

        /// <summary>
        /// Creates a <see cref="ESensConfig"/> configuration data object from a set of given <paramref name="properties"/>.
        /// </summary>
        /// <param name="properties">The custom defined properties.</param>
        /// <returns></returns>
        public static ESensConfig From(IDictionary<string, string> properties) => new(
            properties.ReadOptionalProperty("SmlScheme", "iso6523-actorid-upis").Trim('.'),
            properties.ReadOptionalProperty("SmpServerDomainName", "isaitb.acc.edelivery.tech.ec.europa.eu").Trim('.'));
    }

    /// <summary>
    /// Retrieves the SMP meta data <see cref="XmlDocument"/> for a given <paramref name="party"/> using a given <paramref name="properties"/>.
    /// </summary>
    /// <param name="party">The party identifier to select the right SMP meta-data.</param>
    /// <param name="properties">The information properties specified in the <see cref="SendingProcessingMode"/> for this profile.</param>
    /// <param name="cancellation"></param>
    public async Task<XmlDocument> RetrieveSmpMetaDataAsync(
        Model.Core.Party party,
        IDictionary<string, string> properties, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(party);

        ArgumentNullException.ThrowIfNull(properties);

        if (party.PrimaryPartyId == null)
        {
            throw new InvalidOperationException("Given invalid 'ToParty'; requires a 'PartyId'");
        }

        var smpUrl = CreateSmpServerUrl(party, ESensConfig.From(properties));

        return await RetrieveSmpMetaData(smpUrl);
    }

    private static Uri CreateSmpServerUrl(Model.Core.Party party, ESensConfig config)
    {
        var hashedPartyId = CalculateMD5Hash(party.PrimaryPartyId);

        var host = $"b-{hashedPartyId}.{config.SmlScheme}.{config.SmpServerDomainName}";
        var path = $"{config.SmlScheme}::{party.PrimaryPartyId}/services/{DocumentIdentifierScheme}::{DocumentIdentifier}";


        var builder = new UriBuilder
        {
            Host = host,
            // DotNetBug: Colons need to be Percentage encoded in final Url for SMP lookup. 
            // Uri/HttpClient.GetAsync components encodes # but not : so we need to do it manually.
            Path = HttpUtility.UrlEncode(path)
        };

        return builder.Uri;
    }

    private static string CalculateMD5Hash(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hash = MD5.HashData(inputBytes);

        var sb = new StringBuilder();

        foreach (var t in hash)
        {
            sb.Append(t.ToString("X2"));
        }

        return sb.ToString();
    }

    private async Task<XmlDocument> RetrieveSmpMetaData(Uri smpServerUri)
    {
        _logger.LogInformation("Contacting SMP server at {SmpServerUri} to retrieve meta-data", smpServerUri);

        var response = await _httpClient.GetAsync(smpServerUri);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new HttpListenerException((int)response.StatusCode, "Unexpected result returned from SMP Service");
        }

        if (response.Content.Headers.ContentType?.MediaType?.Contains("xml", StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new NotSupportedException($"An XML response was expected from the SMP server instead of {response.Content.Headers.ContentType?.MediaType}");
        }

        var result = new XmlDocument();
        result.Load(await response.Content.ReadAsStreamAsync());

        return result;
    }

    /// <summary>
    /// Complete the <paramref name="pmode"/> with the SMP metadata that is present in the <paramref name="smpMetaData"/> <see cref="XmlDocument"/>
    /// </summary>
    /// <param name="pmode"></param>
    /// <param name="smpMetaData"></param>
    /// <returns></returns>
    /// 
    public DynamicDiscoveryResult DecoratePModeWithSmpMetaData(SendingProcessingMode pmode, XmlDocument smpMetaData)
    {
        var endpoint = SelectServiceEndpointNode(smpMetaData);
        var certificateNode = endpoint.SelectSingleNode("*[local-name()='Certificate']");

        _logger.LogDebug("Decorate SendingPMode {PModeId} with SMP response from ESens SMP Server", pmode.Id);

        OverwritePushProtocolUrl(pmode, endpoint);
        DecorateMessageProperties(pmode, smpMetaData);
        OverwriteCollaborationServiceAction(pmode, smpMetaData);

        if (certificateNode != null)
        {
            OverwriteToParty(pmode, certificateNode);
            OverwriteEncryptionCertificate(pmode, certificateNode);
        }
        else
        {
            _logger.LogTrace("Don't override MessagePackaging.PartyInfo.ToParty because no <Certificate/> element found in SMP response");
            _logger.LogTrace("Don't override Encryption Certificate because no <Certificate/> element found in SMP response");
        }

        // TODO: should we specify to override the ToParty here also?
        return DynamicDiscoveryResult.Create(pmode);
    }

    private static XmlNode SelectServiceEndpointNode(XmlNode smpMetaData)
    {
        var serviceEndpointList =
            smpMetaData.SelectSingleNode("//*[local-name()='ServiceEndpointList']") ?? throw new InvalidDataException(
                "No <ServiceEndpointList/> element found in the SMP meta-data");
        const string SupportedTransportProfile = "bdxr-transport-ebms3-as4-v1p0";
        var endPoint =
            smpMetaData.SelectSingleNode(
                $"//*[local-name()='ServiceEndpointList']/*[local-name()='Endpoint' and @transportProfile='{SupportedTransportProfile}']");

        var foundTransportProfiles =
            serviceEndpointList
                .ChildNodes
                .Cast<XmlNode>()
                .Select(n => n?.Attributes?["transportProfile"]?.Value)
                .Where(p => p != null);

        if (endPoint == null)
        {
            var foundTransportProfilesFormatted =
                foundTransportProfiles.Any()
                    ? $"; did found: {string.Join(", ", foundTransportProfiles)} transport profiles"
                    : "; no other transport profiles were found";

            throw new InvalidDataException(
                "No <Endpoint/> element in an <ServiceEndpointList/> element found in SMP meta-data "
                + $"where the @transportProfile attribute is {SupportedTransportProfile}"
                + foundTransportProfilesFormatted);
        }

        return endPoint;
    }

    private void OverwritePushProtocolUrl(SendingProcessingMode pmode, XmlNode endpoint)
    {
        pmode.PushConfiguration ??= new PushConfiguration();
        pmode.PushConfiguration.Protocol = pmode.PushConfiguration.Protocol ?? new Protocol();
        pmode.PushConfiguration.Protocol.Url = SelectEndpointAddress(endpoint).InnerText;
    }

    private XmlNode SelectEndpointAddress(XmlNode endpoint)
    {
        var address = endpoint.SelectSingleNode("*[local-name()='EndpointReference']/*[local-name()='Address']")
            ?? throw new InvalidDataException("No ServiceEndpointList/Endpoint/EndpointReference/Address element found in SMP meta-data");

        _logger.LogTrace("Override SendingPMode.PushConfiguration.Protocol with {{Url={InnerText}}}", address.InnerText);
        return address;
    }

    private void DecorateMessageProperties(SendingProcessingMode pmode, XmlDocument smpMetaData)
    {
        bool IsFinalReceipient(MessageProperty p)
        {
            return p?.Name?.Equals("finalRecipient", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        bool IsOriginalSender(MessageProperty p)
        {
            return p?.Name?.Equals("originalSender", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        pmode.MessagePackaging ??= new SendMessagePackaging();
        pmode.MessagePackaging.MessageProperties = pmode.MessagePackaging.MessageProperties ?? [];
        pmode.MessagePackaging.MessageProperties.RemoveAll(IsFinalReceipient);
        pmode.MessagePackaging.MessageProperties.Add(CreateFinalRecipient(smpMetaData));
        if (!pmode.MessagePackaging.MessageProperties.Any(IsOriginalSender))
        {
            pmode.MessagePackaging.MessageProperties.Add(CreateOriginalSender());
        }
    }

    private MessageProperty CreateFinalRecipient(XmlNode smpMetaData)
    {
        var node = smpMetaData.SelectSingleNode("//*[local-name()='ParticipantIdentifier']") ?? throw new InvalidDataException("No ParticipantIdentifier element found in SMP meta-data");
        var schemeAttribute =
            node.Attributes?
                .OfType<XmlAttribute>()
                .FirstOrDefault(a => a.Name.Equals("scheme", StringComparison.OrdinalIgnoreCase))
                ?.Value;

        _logger.LogTrace("Add MessageProperty 'finalRecipient' to SendingPMode");
        return new MessageProperty
        {
            Name = "finalRecipient",
            Value = node.InnerText,
            Type = schemeAttribute
        };
    }

    private MessageProperty CreateOriginalSender()
    {
        _logger.LogTrace("Add MessageProperty 'originalSender' to SendingPMode");
        return new MessageProperty
        {
            Name = "originalSender",
            Value = "urn:oasis:names:tc:ebcore:partyid-type:unregistered:C1"
        };
    }

    private void OverwriteCollaborationServiceAction(SendingProcessingMode pmode, XmlDocument smpMetaData)
    {
        pmode.MessagePackaging ??= new SendMessagePackaging();
        pmode.MessagePackaging.CollaborationInfo = pmode.MessagePackaging.CollaborationInfo ?? new CollaborationInfo();
        pmode.MessagePackaging.CollaborationInfo.Action = SelectCollaborationAction(smpMetaData);
        pmode.MessagePackaging.CollaborationInfo.Service = SelectCollaborationService(smpMetaData);
    }

    private string SelectCollaborationAction(XmlNode smpMetaData)
    {
        var documentIdentifier =
            smpMetaData.SelectSingleNode(
                "//*[local-name()='ServiceInformation']/*[local-name()='DocumentIdentifier']") ?? throw new InvalidDataException(
                "Unable to complete CollaborationInfo: no ServiceInformation/DocumentIdentifier element not found in SMP metadata");
        _logger.LogTrace("Override SendingPMode.MessagingPackaging.CollaborationInfo with {{Action={InnerText}}}", documentIdentifier.InnerText);
        return documentIdentifier.InnerText;
    }

    private Service SelectCollaborationService(XmlNode smpMetaData)
    {
        var processIdentifier =
            smpMetaData.SelectSingleNode(
                "//*[local-name()='ProcessList']/*[local-name()='Process']/*[local-name()='ProcessIdentifier']") ?? throw new InvalidDataException(
                "Unable to complete CollaborationInfo: ProcessList/ProcessIdentifier element not found in SMP metadata");
        var serviceValue = processIdentifier.InnerText;
        var serviceType = processIdentifier
                .Attributes
                ?.OfType<XmlAttribute>()
                .FirstOrDefault(a => a.Name.Equals("scheme", StringComparison.OrdinalIgnoreCase))
                ?.Value;

        _logger.LogTrace("Override SendingPMode.MessagingPackaging.CollaborationInfo with {{ServiceType={ServiceType}, ServiceValue={ServiceValue}}}", serviceType, serviceValue);
        return new Service
        {
            Value = serviceValue,
            Type = serviceType
        };
    }

    private void OverwriteToParty(SendingProcessingMode pmode, XmlNode certificateNode)
    {
        pmode.MessagePackaging ??= new SendMessagePackaging();
        pmode.MessagePackaging.PartyInfo = pmode.MessagePackaging.PartyInfo ?? new PartyInfo();

        var cert = new X509Certificate2(rawData: Convert.FromBase64String(certificateNode.InnerText));

        const string ResponderRole = "http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/200704/responder";
        _logger.LogTrace("Override MessagingPackaging.PartyInfo.ToParty with {{Role={ResponderRole}}}", ResponderRole);

        pmode.MessagePackaging.PartyInfo.ToParty = new Party(
            role: ResponderRole,
            partyId: new PartyId(
                id: cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false))
            {
                Type = "urn:oasis:names:tc:ebcore:partyid-type:unregistered"
            });
    }

    private void OverwriteEncryptionCertificate(SendingProcessingMode pmode, XmlNode certificateNode)
    {
        _logger.LogTrace("Override SendingPMode.Security.Encryption with CertificateType=PublicKeyCertificate");
        pmode.Security ??= new Model.PMode.Security();
        pmode.Security.Encryption = pmode.Security.Encryption ?? new Encryption();

        pmode.Security.Encryption.CertificateType = PublicKeyCertificateChoiceType.PublicKeyCertificate;
        pmode.Security.Encryption.EncryptionCertificateInformation = new PublicKeyCertificate
        {
            Certificate = certificateNode.InnerText
        };
    }
}
