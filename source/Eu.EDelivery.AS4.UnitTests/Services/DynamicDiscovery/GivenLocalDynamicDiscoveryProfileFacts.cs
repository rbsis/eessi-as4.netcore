using System.Xml;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Services.DynamicDiscovery;
using Eu.EDelivery.AS4.UnitTests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Party = Eu.EDelivery.AS4.Model.Core.Party;
using PartyId = Eu.EDelivery.AS4.Model.Core.PartyId;

namespace Eu.EDelivery.AS4.UnitTests.Services.DynamicDiscovery;

public class GivenLocalDynamicDiscoveryProfileFacts : GivenDatastoreFacts
{
    [Fact]
    public async Task RetrieveSmpResponseFromDatastore()
    {
        // Arrange
        var fixture = new Party("role", new PartyId(Guid.NewGuid().ToString(), "type"));
        var expected = new SmpConfiguration
        {
            PartyRole = fixture.Role,
            ToPartyId = fixture.PrimaryPartyId,
            PartyType = "type"
        };

        InsertSmpResponse(expected);

        var sut = new LocalDynamicDiscoveryProfile(NullLogger<LocalDynamicDiscoveryProfile>.Instance, Default.NewDatastoreRepository(this));

        // Act
        var actualDoc = await sut.RetrieveSmpMetaDataAsync(fixture, new Dictionary<string, string>(), CancellationToken.None);

        // Assert
        var actual = await AS4XmlSerializer.FromStringAsync<SmpConfiguration>(actualDoc.OuterXml, CancellationToken.None);
        Assert.NotNull(actual);
        Assert.Equal(expected.ToPartyId, actual.ToPartyId);
    }

    private void InsertSmpResponse(SmpConfiguration smpConfiguration)
    {
        using var context = GetDataStoreContext();
        context.SmpConfigurations.Add(smpConfiguration);
        context.SaveChanges();
    }

    [Fact]
    public void DecorateMandatoryInfoToSendingPMode()
    {
        // Arrange
        var smpResponse = new SmpConfiguration
        {
            PartyRole = "role",
            ToPartyId = Guid.NewGuid().ToString(),
            PartyType = "type",
            Url = "http://some/url"
        };

        var doc = new XmlDocument();
        doc.LoadXml(AS4XmlSerializer.ToString(smpResponse));

        var pmode = new SendingProcessingMode();
        var sut = new LocalDynamicDiscoveryProfile(NullLogger<LocalDynamicDiscoveryProfile>.Instance, Default.NewDatastoreRepository(this));

        // Act
        var actual = sut.DecoratePModeWithSmpMetaData(pmode, doc).CompletedSendingPMode;

        // Assert
        Assert.NotNull(actual.PushConfiguration);
        Assert.Equal(smpResponse.Url, actual.PushConfiguration.Protocol.Url);
    }

    [Fact]
    public void DecorateButNotRecreatePushConfiguration()
    {
        // Arrange
        var smpResponse = new SmpConfiguration
        {
            PartyRole = "role",
            ToPartyId = Guid.NewGuid().ToString(),
            PartyType = "type",
            Url = "http://some/url"
        };

        var push = new PushConfiguration
        {
            TlsConfiguration = new TlsConfiguration
            {
                CertificateType = TlsCertificateChoiceType.PrivateKeyCertificate,
                ClientCertificateInformation = new ClientCertificateReference()
            }
        };
        var fixture = new SendingProcessingMode { PushConfiguration = push };

        // Act
        var result = ExerciseDecorate(fixture, smpResponse);

        // Assert
        Assert.NotNull(result.PushConfiguration);
        Assert.Same(push, result.PushConfiguration);
        Assert.Equal(smpResponse.Url, push.Protocol.Url);
        Assert.Same(
            push.TlsConfiguration.ClientCertificateInformation,
            result.PushConfiguration.TlsConfiguration.ClientCertificateInformation);
    }

    [Fact]
    public void DecorateNotRecreateCollaborationInfo()
    {
        // Arrange
        var smpResponse = new SmpConfiguration
        {
            PartyRole = "role",
            ToPartyId = Guid.NewGuid().ToString(),
            PartyType = "type",
            Url = "http://some/url"
        };
        var collaboration = new CollaborationInfo
        {
            AgreementReference = new AgreementReference
            {
                Value = "http://eu.europe.org/agreements"
            }
        };

        var fixture = new SendingProcessingMode
        {
            MessagePackaging = new SendMessagePackaging
            {
                CollaborationInfo = collaboration
            }
        };

        // Act
        var result = ExerciseDecorate(fixture, smpResponse);

        // Assert
        Assert.NotNull(result.MessagePackaging.CollaborationInfo);
        Assert.Same(collaboration, result.MessagePackaging.CollaborationInfo);
        Assert.Equal(
            collaboration.AgreementReference.Value,
            result.MessagePackaging.CollaborationInfo.AgreementReference.Value);
    }

    [Fact]
    public void DontTouchSigningDuringDecoration()
    {
        // Arrange
        var smpResponse = new SmpConfiguration
        {
            PartyRole = "role",
            ToPartyId = Guid.NewGuid().ToString(),
            PartyType = "type",
            Url = "http://some/url",
            EncryptionEnabled = true
        };
        var fixture = new SendingProcessingMode
        {
            Security =
            {
                Signing = { IsEnabled = true }
            }
        };

        // Act
        var result = ExerciseDecorate(fixture, smpResponse);

        // Assert
        Assert.Same(fixture.Security.Signing, result.Security.Signing);
        Assert.True(result.Security.Signing.IsEnabled);
    }

    private SendingProcessingMode ExerciseDecorate(SendingProcessingMode pmode, SmpConfiguration smpResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(AS4XmlSerializer.ToString(smpResponse));

        var sut = new LocalDynamicDiscoveryProfile(NullLogger<LocalDynamicDiscoveryProfile>.Instance, Default.NewDatastoreRepository(this));
        return sut.DecoratePModeWithSmpMetaData(pmode, doc).CompletedSendingPMode;
    }
}
