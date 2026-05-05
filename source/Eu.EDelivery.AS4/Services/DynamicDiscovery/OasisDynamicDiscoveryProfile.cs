using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.PMode;
using Heijden.Dns.Portable;
using Heijden.DNS;
using Microsoft.Extensions.Logging;
using ArgumentException = System.ArgumentException;
using Party = Eu.EDelivery.AS4.Model.Core.Party;
using TransportType = Heijden.DNS.TransportType;

namespace Eu.EDelivery.AS4.Services.DynamicDiscovery;

/// <summary>
/// Dynamic Discovery profile to retrieve a compliant eDelivery SMP profile based on the OASIS BDX Service Metadata Publishers (SMP)
/// to extract information about the unknown receiver MSH. After a successful retrieval, the <see cref="SendingProcessingMode"/> can be extended
/// with the endpoint address, service value/type, action, receiver party and the public encryption certificate of the receiving MSH.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "<Pending>")]
public class OasisDynamicDiscoveryProfile : IDynamicDiscoveryProfile
{
    private readonly ILogger<OasisDynamicDiscoveryProfile> _logger;

    private const string SmpHttpRegexPattern = ".*?(http.*[^!])";
    private static readonly Regex SmpHttpRegex = new(SmpHttpRegexPattern, RegexOptions.Compiled);
    private static readonly HttpClient HttpClient = new();
    private static readonly Resolver DnsResolver = new()
    {
        TransportType = TransportType.Udp,
        Recursion = true,
        Retries = 3,
        UseCache = true
    };

    public OasisDynamicDiscoveryProfile(ILogger<OasisDynamicDiscoveryProfile> logger)
    {
        _logger = logger;
        ServiceProviderDomainName = string.Empty;
    }

    /// <summary>
    /// Gets the environment of the service provider to include in the DNS NAPTR lookup.
    /// </summary>
    [Info("Service provider sub-domain", required: false)]
    [Description("Sub domain of the service provider")]
    // ReSharper disable once UnassignedGetOnlyAutoProperty
    internal string? ServiceProviderSubDomain { get; }

    /// <summary>
    /// Gets the service provider domain name for the DNS NAPTR lookup.
    /// </summary>
    [Info("Service provider domain name", required: true)]
    [Description("Domain name of the service provider")]
    // ReSharper disable once UnassignedGetOnlyAutoProperty
    internal string ServiceProviderDomainName { get; }

    /// <summary>
    /// Gets the document identifier to append to the retrieved SMP URL.
    /// </summary>
    [Info("Document identifier", required: false)]
    [Description("Document identifier to append to the retrieved SMP URL")]
    // ReSharper disable once UnassignedGetOnlyAutoProperty
    internal string? DocumentIdentifier { get; }

    /// <summary>
    /// Gets the document scheme to append to the retrieved SMP URL.
    /// </summary>
    [Info("Document scheme", required: false)]
    [Description("Document scheme to append to the retrieved SMP URL")]
    // ReSharper disable once UnassignedGetOnlyAutoProperty
    internal string? DocumentScheme { get; }

    /// <summary>
    /// Retrieves the SMP meta data <see cref="XmlDocument"/> for a given <paramref name="party"/> using a given <paramref name="properties"/>.
    /// </summary>
    /// <param name="party">The party identifier to select the right SMP meta-data.</param>
    /// <param name="properties">The information properties specified in the <see cref="SendingProcessingMode"/> for this profile.</param>
    /// <param name="cancellation"></param>
    public async Task<XmlDocument> RetrieveSmpMetaDataAsync(Party party, IDictionary<string, string> properties, CancellationToken cancellation)
    {
        if (!party.PartyIds.Any() || party.PartyIds.All(id => id.Type == Maybe<string>.Nothing))
        {
            throw new ArgumentException(
                @"ToParty must have at least one PartyId element with a Type to retrieve the SMP meta-data",
                nameof(party));
        }

        var serviceProviderDomainName = properties.ReadMandatoryProperty(nameof(ServiceProviderDomainName));
        var serviceProviderSubDomain = properties.ReadOptionalProperty(nameof(ServiceProviderSubDomain), string.Empty);

        (var participant, var dnsResponse) =
            QueryDnsNatprRecord(party.PartyIds, serviceProviderSubDomain, serviceProviderDomainName);

        var smpUri = SelectSmpUriFromDnsResponse(dnsResponse);

        var participantIdentifier = participant.Id;
        var participantScheme = participant.Type.UnsafeGet;

        var documentIdentifier = properties.ReadOptionalProperty(nameof(DocumentIdentifier), string.Empty);
        var documentScheme = properties.ReadOptionalProperty(nameof(DocumentScheme), string.Empty);

        if (string.IsNullOrWhiteSpace(documentIdentifier) && string.IsNullOrWhiteSpace(documentScheme))
        {
            var smpRestBindingFromFallback =
                await RetrieveSmpRestBindingFromFallbackAsync(smpUri, participantScheme, participantIdentifier);

            return await CallHttpBinding($"{smpRestBindingFromFallback}");
        }

        if (string.IsNullOrWhiteSpace(documentIdentifier) || string.IsNullOrWhiteSpace(documentScheme))
        {
            throw new ArgumentException(
                @"DocumentIdentifier and DocumentScheme properties should both be specified or unspecified",
                nameof(properties));
        }

        var smpRestBindingFromProperties =
            $"{smpUri}{participantScheme}::{participantIdentifier}/services/{documentScheme}::{documentIdentifier}";

        return await CallHttpBinding(smpRestBindingFromProperties);
    }

    private (Model.Core.PartyId, Response) QueryDnsNatprRecord(
        IEnumerable<Model.Core.PartyId> participants,
        string serviceProviderSubDomain,
        string serviceProviderDomainName)
    {
        foreach (var participant in participants.Where(p => p.Type != Maybe<string>.Nothing))
        {
            var participantIdentifier = participant.Id;
            var participantScheme = participant.Type.UnsafeGet;

            var dnsDomainName =
                $"{Base32Encoding.ToString(SHA256.HashData(Encoding.UTF8.GetBytes(participantIdentifier))).TrimEnd('=')}"
                + $".{participantScheme}"
                + $"{(string.IsNullOrWhiteSpace(serviceProviderSubDomain) ? string.Empty : "." + serviceProviderSubDomain)}"
                + $".{serviceProviderDomainName}";

            var dnsResponse = Task.Run(() => DnsResolver.Query(dnsDomainName, QType.NAPTR, QClass.IN)).Result;
            if (dnsResponse.Answers.Count > 0)
            {
                return (participant, dnsResponse);
            }

            _logger.LogDebug("DNS NAPTR query: {DnsDomainName} doesn't result in a DNS NAPTR awnser, try next PartyId", dnsDomainName);
        }

        throw new InvalidDataException(
            "None of the PartyIds in the ToParty result in a DNS NAPTR record");
    }

    private static Uri SelectSmpUriFromDnsResponse(Response dnsResponse)
    {
        var firstMatchedNaptrRecord =
            dnsResponse.Answers
                       .Select(r => r.RECORD)
                       .Cast<RecordNAPTR>()
                       .FirstOrDefault() ?? throw new InvalidDataException(
                "No DNS NAPTR record found to get the SMP REST binding from");
        var matches = SmpHttpRegex.Matches(firstMatchedNaptrRecord.REGEXP);
        if (matches.Count == 0)
        {
            throw new InvalidDataException(
                $"DNS NAPTR record value REGEXP: \"{firstMatchedNaptrRecord.REGEXP}\" doesn't match regular expression: \"{SmpHttpRegexPattern}\"");
        }

        var firstMatch = matches[0];
        if (firstMatch.Groups.Count < 2)
        {
            throw new InvalidDataException(
                $"DNS NAPTR record value REGEXP: \"{firstMatchedNaptrRecord.REGEXP}\" doesn't match regular expression: \"{SmpHttpRegexPattern}\"");
        }

        // First group is always the entire matched string like "!^.*$!http://40.115.23.114:38080/".
        // Second group is always only the matched parts like "http://40.115.23.114:38080/"
        var matched = firstMatch.Groups[1].Value;
        return new Uri(matched);
    }

    private static async Task<Uri> RetrieveSmpRestBindingFromFallbackAsync(Uri smpUri, string participantScheme, string participantIdentifier)
    {
        var smpRestBindingFallback =
            $"{smpUri}{participantScheme}::{participantIdentifier}";

        var smpFallbackRefDoc = await CallHttpBinding(smpRestBindingFallback);

        var ns = new XmlNamespaceManager(smpFallbackRefDoc.NameTable);
        ns.AddNamespace("oasis", "http://docs.oasis-open.org/bdxr/ns/SMP/2016/05");

        var serviceMetadataRefNode = smpFallbackRefDoc.SelectSingleNode(
            "//oasis:ServiceMetadataReferenceCollection/oasis:ServiceMetadataReference", ns) ?? throw new InvalidDataException(
                "No ServiceMetadataReference found in an <ServiceMetadataReferenceCollection/> in the fallback SMP REST binding response");
        var hrefNode =
            (serviceMetadataRefNode
                .Attributes
                ?.OfType<XmlAttribute>()
                .FirstOrDefault(a => a.LocalName == "href")) ?? throw new InvalidDataException(
                "No 'href' XML attribute found in the <ServiceMetadataReference/> element in the fallback SMP REST binding response");
        if (string.IsNullOrWhiteSpace(hrefNode.Value))
        {
            throw new InvalidDataException(
                "No SMP REST binding found in the 'href' XML attribute present in the <ServiceMetadataReference/> element in the fallback SMP REST binding response");
        }

        return new Uri(hrefNode.Value);
    }

    private static async Task<XmlDocument> CallHttpBinding(string binding)
    {
        using var smpResponse = await HttpClient.GetAsync(binding);
        if (!smpResponse.IsSuccessStatusCode)
        {
            throw new WebException(
                $"Calling the SMP server at {binding} doesn't result in an successful response");
        }

        var xmlStream = await smpResponse.Content.ReadAsStreamAsync();

        var smpMetaData = new XmlDocument();
        smpMetaData.Load(xmlStream);
        return smpMetaData;
    }

    /// <summary>
    /// Complete the <paramref name="pmode"/> with the SMP metadata that is present in the <paramref name="smpMetaData"/> <see cref="XmlDocument"/>
    /// </summary>
    /// <param name="pmode">The <see cref="SendingProcessingMode"/> that must be decorated with the SMP metadata</param>
    /// <param name="smpMetaData">An XmlDocument that contains the SMP MetaData that has been received from an SMP server.</param>
    /// <returns>The completed <see cref="SendingProcessingMode"/></returns>
    /// 
    public DynamicDiscoveryResult DecoratePModeWithSmpMetaData(SendingProcessingMode pmode, XmlDocument smpMetaData)
    {
        var ns = new XmlNamespaceManager(smpMetaData.NameTable);
        ns.AddNamespace("oasis", "http://docs.oasis-open.org/bdxr/ns/SMP/2016/05");

        var endpointNode = SelectEndpointNode(smpMetaData, ns);
        OverridePushConfigurationProtocolUrl(pmode, endpointNode, ns);
        OverrideMessageProperties(pmode, smpMetaData, ns);
        OverrideCollaborationAction(pmode, smpMetaData, ns);
        OverrideCollaborationService(pmode, smpMetaData, ns);

        var certificateNode = smpMetaData.SelectSingleNode("//oasis:Certificate", ns);
        if (certificateNode != null)
        {
            var certificateBinaries = certificateNode.InnerText.Replace(" ", "").Replace("\r\n", "");
            OverrideEncryptionCertificate(pmode, certificateBinaries);
            OverrideToParty(pmode, certificateBinaries);

            return DynamicDiscoveryResult.Create(pmode, overrideToParty: true);
        }

        _logger.LogTrace("Don't override MessagePackaging.PartyInfo.ToParty because no <Certificate/> element found in SMP meta-data");
        _logger.LogTrace("Don't override Encryption Certificate because no <Certificate/> element found in SMP meta-data");

        return DynamicDiscoveryResult.Create(pmode);
    }

    private static XmlNode SelectEndpointNode(XmlDocument smpMetaData, XmlNamespaceManager ns)
    {
        // TODO: now the first matched tag is selected while we can select more strictly by matching UserMessages with <Process/> elements.
        var serviceEndpointListNode = smpMetaData.SelectSingleNode("//oasis:ServiceEndpointList", ns) ?? throw new InvalidDataException("No <ServiceEndpointList/> element found in the SMP meta-data");
        const string SupportedTransportProfile = "bdxr-transport-ebms3-as4-v1p0";
        var endpointNode =
            serviceEndpointListNode.SelectSingleNode($"//oasis:Endpoint[@transportProfile='{SupportedTransportProfile}']", ns);

        if (endpointNode == null)
        {
            var foundTransportProfiles =
                serviceEndpointListNode.ChildNodes
                    .Cast<XmlNode>()
                    .Select(n => n?.Attributes?["transportProfile"]?.Value)
                    .Where(p => p != null);

            var foundTransportProfilesFormatted =
                foundTransportProfiles.Any()
                    ? $"; did found: {string.Join(", ", foundTransportProfiles)} transport profiles"
                    : "; no other transport profiles were found";

            throw new InvalidDataException(
                "No <Endpoint/> element in an <ServiceEndpointList/> element found in SMP meta-data "
                + $"where the @transportProfile attribute is {SupportedTransportProfile} {foundTransportProfilesFormatted}");
        }

        return endpointNode;
    }

    private void OverridePushConfigurationProtocolUrl(SendingProcessingMode pmode, XmlNode endpointNode, XmlNamespaceManager ns)
    {
        var endpointUriNode = endpointNode.SelectSingleNode("//oasis:EndpointURI", ns) ?? throw new InvalidDataException(
                "No <EndpointURI/> element in an ServiceEndpointList.Endpoint element found in SMP meta-data to complete SendingPMode.PushConfiguration.Protocol.Url");
        var endpointUri = endpointUriNode.InnerText.Replace(" ", "").Replace("\r\n", "");

        _logger.LogTrace("Override SendingPMode.PushConfiguration.Protocol.Url with {EndpointUri}", endpointUri);
        pmode.PushConfiguration ??= new PushConfiguration();
        pmode.PushConfiguration.Protocol = pmode.PushConfiguration.Protocol ?? new Protocol();
        pmode.PushConfiguration.Protocol.Url = endpointUri;
    }

    private void OverrideCollaborationAction(SendingProcessingMode pmode, XmlDocument smpMetaData, XmlNamespaceManager ns)
    {
        var documentIdentifierNode = smpMetaData.SelectSingleNode("//oasis:ServiceInformation/oasis:DocumentIdentifier", ns) ?? throw new InvalidDataException(
                "No <DocumentIdentifier/> element in an <ServiceInformation/> element found in SMP meta-data to complete ebMS Action");
        var documentScheme =
            documentIdentifierNode.Attributes
                ?.OfType<XmlAttribute>()
                .FirstOrDefault(a => a.Name.Equals("scheme", StringComparison.OrdinalIgnoreCase))
                ?.Value;

        if (string.IsNullOrEmpty(documentScheme))
        {
            throw new InvalidDataException(
                "No 'scheme' XML attribute found in <DocumentIdentifier/> element in SMP meta-data to complete ebMS Action");
        }

        var action = $"{documentScheme}::{documentIdentifierNode.InnerText}";
        _logger.LogTrace("Override SendingPMode.MessagePackaging.CollaborationInfo.Action with {Action}", action);

        pmode.MessagePackaging ??= new SendMessagePackaging();
        pmode.MessagePackaging.CollaborationInfo = pmode.MessagePackaging.CollaborationInfo ?? new CollaborationInfo();
        pmode.MessagePackaging.CollaborationInfo.Action = action;
    }

    private void OverrideCollaborationService(SendingProcessingMode pmode, XmlDocument smpMetaData, XmlNamespaceManager ns)
    {
        var processIdentifierNode =
            smpMetaData.SelectSingleNode("//oasis:ProcessList/oasis:Process/oasis:ProcessIdentifier", ns) ?? throw new InvalidDataException(
                "No <ProcessIdentifier/> in an ProcessList.Process element found in SMP meta-data to complete ebMS Service");
        var serviceType =
            processIdentifierNode.Attributes
                ?.OfType<XmlAttribute>()
                .FirstOrDefault(a => a.Name.Equals("scheme", StringComparison.OrdinalIgnoreCase))
                ?.Value;

        _logger.LogTrace(
            "Override SendingPMode.MessagePackaging.CollaborationInfo.Service with {{Value={ProcessIdentifier}, Type={ServiceType}}}",
            processIdentifierNode.InnerText,
            serviceType);

        pmode.MessagePackaging ??= new SendMessagePackaging();
        pmode.MessagePackaging.CollaborationInfo = pmode.MessagePackaging.CollaborationInfo ?? new CollaborationInfo();
        pmode.MessagePackaging.CollaborationInfo.Service = new Service { Value = processIdentifierNode.InnerText, Type = serviceType };
    }

    private void OverrideMessageProperties(SendingProcessingMode pmode, XmlNode smpMetaData, XmlNamespaceManager ns)
    {
        bool IsFinalRecipient(MessageProperty p)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(p?.Name, "finalRecipient");
        }

        bool IsOriginalSender(MessageProperty p)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(p?.Name, "originalSender");
        }

        pmode.MessagePackaging ??= new SendMessagePackaging();
        pmode.MessagePackaging.MessageProperties = pmode.MessagePackaging.MessageProperties ?? [];
        pmode.MessagePackaging.MessageProperties.RemoveAll(IsFinalRecipient);
        pmode.MessagePackaging.MessageProperties.Add(CreateFinalRecipient(smpMetaData, ns));
        if (!pmode.MessagePackaging.MessageProperties.Any(IsOriginalSender))
        {
            pmode.MessagePackaging.MessageProperties.Add(CreateOriginalSender());
        }
    }

    private MessageProperty CreateFinalRecipient(XmlNode smpMetaData, XmlNamespaceManager ns)
    {
        var participantIdentifierNode =
            smpMetaData.SelectSingleNode("//oasis:ServiceInformation/oasis:ParticipantIdentifier", ns) ?? throw new InvalidDataException("No ParticipantIdentifier element found in SMP meta-data to complete 'finalRecipient' MessageProperty");
        var participantIdentifier =
            participantIdentifierNode.InnerText.Trim();


        var schemeAttribute = participantIdentifierNode.Attributes
                ?.OfType<XmlAttribute>()
                .FirstOrDefault(a => StringComparer.OrdinalIgnoreCase.Equals(a.Name, "scheme"))
                ?.Value;

        _logger.LogTrace("Add MessageProperty 'finalRecipient' = '{ParticipantIdentifier}' to SendingPMode", participantIdentifier);
        return new MessageProperty
        {
            Name = "finalRecipient",
            Value = participantIdentifier,
            Type = schemeAttribute
        };
    }

    private MessageProperty CreateOriginalSender()
    {
        const string DefaultUrnTypeValueC1 = "urn:oasis:names:tc:ebcore:partyid-type:unregistered:C1";
        _logger.LogTrace("Add MessageProperty 'originalSender'= '{DefaultUrnTypeValueC1}' to SendingPMode", DefaultUrnTypeValueC1);

        return new MessageProperty
        {
            Name = "originalSender",
            Value = DefaultUrnTypeValueC1
        };
    }

    private void OverrideEncryptionCertificate(SendingProcessingMode pmode, string certificateBinaries)
    {
        _logger.LogTrace("Override SendingPMode.Security.Encryption with Certificate=PublicKeyCertificate");
        pmode.Security ??= new Model.PMode.Security();
        pmode.Security.Encryption = pmode.Security.Encryption ?? new Encryption();
        pmode.Security.Encryption.CertificateType = PublicKeyCertificateChoiceType.PublicKeyCertificate;
        pmode.Security.Encryption.EncryptionCertificateInformation = new PublicKeyCertificate { Certificate = certificateBinaries };
    }

    private void OverrideToParty(SendingProcessingMode pmode, string certificateBinaries)
    {
        const string DefaultResponderRole = "http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/200704/responder";
        const string DefaultUrnTypeValue = "urn:oasis:names:tc:ebcore:partyid-type:unregistered";

        var encryptionCertificate = new X509Certificate2(rawData: Convert.FromBase64String(certificateBinaries));
        var commonName = encryptionCertificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);

        _logger.LogTrace(
            "Override SendingPMode.MessagingPackaging.PartyInfo.ToParty with {{Role={AefaultResponderRole}, PartyId={CommonName}, PartyIdType={DefaultUrnTypeValue}}}",
            DefaultResponderRole,
            commonName,
            DefaultUrnTypeValue);

        pmode.MessagePackaging ??= new SendMessagePackaging();
        pmode.MessagePackaging.PartyInfo = pmode.MessagePackaging.PartyInfo ?? new PartyInfo();
        pmode.MessagePackaging.PartyInfo.ToParty = new Model.PMode.Party
        {
            Role = DefaultResponderRole,
            PartyIds =
            [
                new(commonName) { Type = DefaultUrnTypeValue }
            ]
        };
    }
}
