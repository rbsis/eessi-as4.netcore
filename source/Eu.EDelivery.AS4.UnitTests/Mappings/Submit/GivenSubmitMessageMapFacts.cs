using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Mappings.PMode;
using Eu.EDelivery.AS4.Mappings.Submit;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.UnitTests.Common;
using FsCheck;
using FsCheck.Xunit;
using AgreementReference = Eu.EDelivery.AS4.Model.Core.AgreementReference;
using CollaborationInfo = Eu.EDelivery.AS4.Model.PMode.CollaborationInfo;
using Party = Eu.EDelivery.AS4.Model.PMode.Party;
using PartyId = Eu.EDelivery.AS4.Model.PMode.PartyId;
using PartyInfo = Eu.EDelivery.AS4.Model.PMode.PartyInfo;
using Service = Eu.EDelivery.AS4.Model.Core.Service;

namespace Eu.EDelivery.AS4.UnitTests.Mappings.Submit;

public class GivenSubmitMessageMapFacts
{
    private static readonly Lazy<IdentifierFactory> _lazyIdentifierFactory = new(() => new IdentifierFactory(StubConfig.Default));
    private static readonly Lazy<SendingPModeMap> _lazySendingPModeMap = new(() => new(_lazyIdentifierFactory.Value));

    private readonly SubmitMessageMap _sut = new(_lazyIdentifierFactory.Value, _lazySendingPModeMap.Value);

    [Fact]
    public void CreateUserMessageFromSubmitMessage()
    {
        // Arrange
        const string SubmitXml =
            @"<?xml version=""1.0""?>
                <SubmitMessage xmlns=""urn:cef:edelivery:eu:as4:messages"">
                  <MessageInfo>
                    <MessageId>F4840B69-8057-40C9-8530-EC91F946C3BF</MessageId>
                  </MessageInfo>
                  <Collaboration>
                    <AgreementRef>
                      <Value>eu.europe.agreements</Value>
                      <PModeId>sample-pmode</PModeId>
                    </AgreementRef>
                  </Collaboration>
                  <MessageProperties>
                    <MessageProperty>
                      <Name>Payloads</Name>
                      <Type>Metadata</Type>
                      <Value>2</Value>
                    </MessageProperty>
                  </MessageProperties>
                  <Payloads>
                    <Payload>
                      <Id>earth</Id>
                      <MimeType>image/jpeg</MimeType>
                      <Location>file:///messages\attachments\earth.jpg</Location>
                      <PayloadProperties/>
                    </Payload>
                    <Payload>
                      <Id>xml-sample</Id>
                      <MimeType>application/xml</MimeType>
                      <Location>file:///messages\attachments\sample.xml</Location>
                      <PayloadProperties>
                        <PayloadProperty>
                          <Name>Important</Name>
                          <Value>Yes</Value>
                        </PayloadProperty>
                      </PayloadProperties>
                    </Payload>
                  </Payloads>
                </SubmitMessage>";

        var submit = AS4XmlSerializer.FromString<SubmitMessage>(SubmitXml);
        var sendingPMode = new SendingProcessingMode();

        // Act
        var result = _sut.CreateUserMessage(submit!, sendingPMode);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Maybe.Nothing<AgreementReference>(), result.CollaborationInfo.AgreementReference);
        Assert.Single(result.MessageProperties);
        Assert.True(result.PayloadInfo.Count() == 2, "expected 2 part infos");
    }

    [Property]
    public Property CreatesCollaborationInfoFromSubmitCollaboration(
        NonEmptyString action,
        string pmodeId,
        NonEmptyString agreementValue,
        string agreementType,
        NonEmptyString serviceValue,
        string serviceType,
        NonEmptyString conversationId)
    {
        // Arrange
        var submit = new SubmitMessage
        {
            Collaboration =
            {
                Action = action.Get,
                AgreementRef = new()
                {
                    Value = agreementValue.Get,
                    RefType = agreementType,
                    PModeId = pmodeId
                },
                Service = new()
                {
                    Value = serviceValue.Get,
                    Type = serviceType
                },
                ConversationId = conversationId.Get
            }
        };
        var sendingPMode = new SendingProcessingMode();

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        var actual = result.CollaborationInfo;

        return actual.Action.Equals(action.Get).Label("equal action")
            .And(actual.Service.Value.Equals(serviceValue.Get).Label("equal service value"))
            .And(actual.AgreementReference.UnsafeGet.Value.Equals(agreementValue.Get).Label("equal agreement value"))
            .And(actual.ConversationId.Equals(conversationId.Get).Label("equal conversation id"));
    }

    [Property]
    public void UseTestDefaultsWhenSubmitCollaborationIsIncomplete(string serviceType)
    {
        // Arrange
        var submit = new SubmitMessage
        {
            Collaboration =
            {
                Action = null,
                Service = new() { Value = null, Type = serviceType }
            }
        };
        var sendingPMode = new SendingProcessingMode
        {
            MessagePackaging = { CollaborationInfo = null }
        };

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        Assert.True(result.IsTest);
        Assert.Equal(AS4.Model.Core.CollaborationInfo.DefaultTest, result.CollaborationInfo);
    }

    [Fact]
    public void FailsWhenSubmitTriesToOverrideAction()
    {
        // Arrange
        var submit = new SubmitMessage
        {
            Collaboration = { Action = Guid.NewGuid().ToString() }
        };
        var sendingPMode = new SendingProcessingMode
        {
            AllowOverride = false,
            MessagePackaging =
            {
                CollaborationInfo = new CollaborationInfo
                {
                    Action = Guid.NewGuid().ToString()
                }
            }
        };

        // Act / Assert
        Assert.Throws<NotSupportedException>(
            () => _sut.CreateUserMessage(submit, sendingPMode));
    }

    public enum Mapped { Submit, PMode, Default }

    public static IEnumerable<object?[]> SubmitMappingFixtures =
    [
        [false, null, null, Mapped.Default],
        [true, null, null, Mapped.Default],
        [false, null, Guid.NewGuid().ToString(), Mapped.PMode],
        [true, null, Guid.NewGuid().ToString(), Mapped.PMode],
        [false, Guid.NewGuid().ToString(), null, Mapped.Submit],
        [true, Guid.NewGuid().ToString(), null, Mapped.Submit],
        [true, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Mapped.Submit]
    ];

    [Theory]
    [MemberData(nameof(SubmitMappingFixtures))]
    public void CreatesAgreementReferenceFromEitherSubmitOrSendingPMode(
        bool allowOverride,
        string? submitAgreement,
        string? pmodeAgreement,
        Mapped expected)
    {
        // Arrange
        var submit = new SubmitMessage
        {
            Collaboration = { AgreementRef = new() { Value = submitAgreement } }
        };
        var sendingPMode = new SendingProcessingMode
        {
            AllowOverride = allowOverride,
            MessagePackaging =
            {
                CollaborationInfo = new CollaborationInfo
                {
                    AgreementReference = { Value = pmodeAgreement }
                }
            }
        };

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        var actual =
            result.CollaborationInfo
                  .AgreementReference
                  .Select(a => GetMapped(a.Value, pmodeAgreement, submitAgreement))
                  .GetOrElse(Mapped.Default);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FailsWhenSubmitTriesToOverrideAgreementReference()
    {
        // Arrange
        var submit = new SubmitMessage
        {
            Collaboration = { AgreementRef = new() { Value = Guid.NewGuid().ToString() } }
        };
        var sendingPMode = new SendingProcessingMode
        {
            AllowOverride = false,
            MessagePackaging =
            {
                CollaborationInfo = new CollaborationInfo
                {
                    AgreementReference = { Value = Guid.NewGuid().ToString() }
                }
            }
        };

        // Act / Assert
        Assert.Throws<NotSupportedException>(
            () => _sut.CreateUserMessage(submit, sendingPMode));
    }

    [Theory]
    [MemberData(nameof(SubmitMappingFixtures))]
    public void CreatesServiceFromEitherSubmitOrSendingPMode(
        bool allowOverride,
        string? submitService,
        string? pmodeService,
        Mapped expected)
    {
        // Arrange
        var submit = new SubmitMessage
        {
            Collaboration =
            {
                Service = new() { Value = submitService }
            }
        };
        var sendingPMode = new SendingProcessingMode
        {
            AllowOverride = allowOverride,
            MessagePackaging =
            {
                CollaborationInfo = new()
                {
                    Service = { Value = pmodeService }
                }
            }
        };

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        var userService = result.CollaborationInfo.Service.Value;
        var actual = GetMapped(userService, pmodeService, submitService);

        Assert.Equal(expected, actual);
        Assert.True((actual == Mapped.Default) == (result.CollaborationInfo.Service.Equals(Service.TestService)),
            "fallback on test Service when none in Submit and SendingPMode is defined");
    }

    [Fact]
    public void FailsWhenSubmitTriesToOverrideService()
    {
        // Arrange
        var submit = new SubmitMessage
        {
            Collaboration =
            {
                Service = new() { Value = Guid.NewGuid().ToString() }
            }
        };
        var sendingPMode = new SendingProcessingMode
        {
            MessagePackaging =
            {
                CollaborationInfo = new()
                {
                    Service = { Value = Guid.NewGuid().ToString() }
                }
            }
        };

        // Act / Assert
        Assert.Throws<NotSupportedException>(
            () => _sut.CreateUserMessage(submit, sendingPMode));
    }

    [Theory]
    [MemberData(nameof(SubmitMappingFixtures))]
    public void CreatesFromPartyFromEitherSubmitOrSendingPMode(
        bool allowOverride,
        string? submitFromParty,
        string? pmodeFromParty,
        Mapped expected)
    {
        // Arrange
        var submit = new SubmitMessage
        {
            PartyInfo =
            {
                FromParty = submitFromParty != null
                    ? new AS4.Model.Common.Party
                    {
                        Role = Guid.NewGuid().ToString(),
                        PartyIds = [new AS4.Model.Common.PartyId(submitFromParty)]
                    }
                    : null
            }
        };
        var sendingPMode = new SendingProcessingMode
        {
            AllowOverride = allowOverride,
            MessagePackaging =
            {
                PartyInfo = new PartyInfo
                {
                    FromParty = pmodeFromParty != null
                        ? new Party(Guid.NewGuid().ToString(), new PartyId(pmodeFromParty))
                        : null
                }
            }
        };

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        var actual = GetMapped(result.Sender.PartyIds.First().Id, pmodeFromParty, submitFromParty);

        Assert.Equal(expected, actual);
        Assert.True(
            (actual == Mapped.Default) == (result.Sender.Equals(AS4.Model.Core.Party.DefaultFrom)),
            "fallback on default FromParty when none in Submit and SendingPMode is defined");
    }

    [Fact]
    public void FailsWhenSubmitTriesToOverrideFromParty()
    {
        // Arrange
        var submit = new SubmitMessage
        {
            PartyInfo =
            {
                FromParty = new AS4.Model.Common.Party
                {
                    Role = Guid.NewGuid().ToString(),
                    PartyIds = [new AS4.Model.Common.PartyId(Guid.NewGuid().ToString())]
                }

            }
        };
        var sendingPMode = new SendingProcessingMode
        {
            AllowOverride = false,
            MessagePackaging =
            {
                PartyInfo = new PartyInfo
                {
                    FromParty = new Party(Guid.NewGuid().ToString(), new PartyId(Guid.NewGuid().ToString()))
                }
            }
        };

        // Act / Assert
        Assert.Throws<NotSupportedException>(
            () => _sut.CreateUserMessage(submit, sendingPMode));
    }

    [Theory]
    [MemberData(nameof(SubmitMappingFixtures))]
    public void CreatesToPartyFromEitherSubmitOrSendingPMode(
        bool allowOverride,
        string? submitToParty,
        string? pmodeToParty,
        Mapped expected)
    {
        // Arrange
        var submit = new SubmitMessage
        {
            PartyInfo =
            {
                ToParty = submitToParty != null
                    ? new AS4.Model.Common.Party
                    {
                        Role = Guid.NewGuid().ToString(),
                        PartyIds = [new AS4.Model.Common.PartyId (submitToParty)]
                    }
                    : null
            }
        };
        var sendingPMode = new SendingProcessingMode
        {
            AllowOverride = allowOverride,
            MessagePackaging =
            {
                PartyInfo = new PartyInfo
                {
                    ToParty = pmodeToParty != null
                        ? new Party(Guid.NewGuid().ToString(), new PartyId(pmodeToParty))
                        : null
                }
            }
        };

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        var actual = GetMapped(result.Receiver.PartyIds.First().Id, pmodeToParty, submitToParty);

        Assert.Equal(expected, actual);
        Assert.True(
            (actual == Mapped.Default) == (result.Receiver.Equals(AS4.Model.Core.Party.DefaultTo)),
            "fallback on default ToParty when none in Submit and SendingPMode is defined");
    }

    [Fact]
    public void FailsWhenSubmitTriesToOverrideToParty()
    {
        // Arrange
        var submit = new SubmitMessage
        {
            PartyInfo =
            {
                ToParty = new AS4.Model.Common.Party
                {
                    Role = Guid.NewGuid().ToString(),
                    PartyIds = [new AS4.Model.Common.PartyId(Guid.NewGuid().ToString())]
                }
            }
        };
        var sendingPMode = new SendingProcessingMode
        {
            AllowOverride = false,
            MessagePackaging =
            {
                PartyInfo = new PartyInfo
                {
                    ToParty = new Party(Guid.NewGuid().ToString(), new PartyId(Guid.NewGuid().ToString()))
                }
            }
        };

        // Act / Assert
        Assert.Throws<NotSupportedException>(
            () => _sut.CreateUserMessage(submit, sendingPMode));
    }

    [Theory]
    [MemberData(nameof(SubmitMappingFixtures))]
    public void ResolvesMpcFromEitherSubmitOrSendingPMode(
        bool allowOverride,
        string? submitMpc,
        string pmodeMpc,
        Mapped expected)
    {
        // Arrange
        var submit = new SubmitMessage { MessageInfo = { Mpc = submitMpc } };
        var sendingPMode = new SendingProcessingMode
        {
            AllowOverride = allowOverride,
            MessagePackaging = { Mpc = pmodeMpc }
        };

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        var actual = GetMapped(result.Mpc, pmodeMpc, submitMpc);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FailsWhenSubmitTriesToOverrideMpc()
    {
        // Arrange
        var submit = new SubmitMessage { MessageInfo = { Mpc = Guid.NewGuid().ToString() } };
        var sendingPMode = new SendingProcessingMode
        {
            AllowOverride = false,
            MessagePackaging = { Mpc = Guid.NewGuid().ToString() }
        };

        // Act / Assert
        Assert.Throws<NotSupportedException>(
            () => _sut.CreateUserMessage(submit, sendingPMode));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssignCompressionPropertiesToPayloadsWhenUseCompressionInSendingPModeIsOn(
        bool useCompression)
    {
        // Arrange
        var submit = new SubmitMessage
        {
            Payloads =
            [
                new Payload("xml-payload")
                {
                    PayloadProperties =
                    [
                        new PayloadProperty("DocumentType", "Business Document")
                    ]
                },
                new Payload("image-payload")
            ]
        };
        var sendingPMode = new SendingProcessingMode
        {
            MessagePackaging =
            {
                UseAS4Compression = useCompression
            }
        };

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        Assert.True(2 == result.PayloadInfo.Count(), "expect 2 part infos");
        Assert.All(result.PayloadInfo, p => Assert.StartsWith("cid:", p.Href));
        Assert.True(
            result.PayloadInfo.First().Properties.Count >= 1,
            "original payload property is present");
        Assert.True(
            useCompression == result.PayloadInfo.All(p => p.Properties.ContainsKey("CompressionType")),
            "expect all part infos to have a 'CompressionType' property");
    }


    [Fact]
    public void CombineSubmitAndSendingPModeMessageProperties()
    {
        // Arrange
        var submit = new SubmitMessage
        {
            MessageProperties =
            [
                new AS4.Model.Common.MessageProperty { Name = "originalSender", Type = "Important", Value = "Holodeck" },
                new AS4.Model.Common.MessageProperty { Name = "finalRecipient", Value = "AS4.NET" },
            ]
        };
        var sendingPMode = new SendingProcessingMode
        {
            MessagePackaging =
            {
                MessageProperties =
                [
                    new() { Name = "capability", Type = "info", Value = "receiving" },
                    new() { Name = "endpoint", Value = "international" },
                ]
            }
        };

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        Assert.Collection(
            result.MessageProperties,
            p => Assert.Equal(("originalSender", "Important", "Holodeck"), (p.Name, p.Type, p.Value)),
            p => Assert.Equal(("finalRecipient", "AS4.NET"), (p.Name, p.Value)),
            p => Assert.Equal(("capability", "info", "receiving"), (p.Name, p.Type, p.Value)),
            p => Assert.Equal(("endpoint", "international"), (p.Name, p.Value)));
    }

    [Fact]
    public void UseSubmitMessagePropertiesWhenSendingPModeMessagePropertiesAreEmpty()
    {
        // Arrange
        var submit = new SubmitMessage
        {
            MessageProperties =
            [
                new AS4.Model.Common.MessageProperty { Name = "originalSender", Type = "Important", Value = "Holodeck" },
                new AS4.Model.Common.MessageProperty { Name = "finalRecipient", Value = "AS4.NET" },
            ]
        };
        var sendingPMode = new SendingProcessingMode
        {
            MessagePackaging =
            {
                MessageProperties = null
            }
        };

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        Assert.Collection(
            result.MessageProperties,
            p => Assert.Equal(("originalSender", "Important", "Holodeck"), (p.Name, p.Type, p.Value)),
            p => Assert.Equal(("finalRecipient", "AS4.NET"), (p.Name, p.Value)));

    }

    [Fact]
    public void UseSendingPModeMessagePropertiesWhenSubmitMessagePropertiesAreEmpty()
    {
        // Arrange
        var submit = new SubmitMessage
        {
            MessageProperties = []
        };
        var sendingPMode = new SendingProcessingMode
        {
            MessagePackaging =
            {
                MessageProperties =
                [
                    new() { Name = "capability", Type = "info", Value = "receiving" },
                    new() { Name = "endpoint", Value = "international" },
                ]
            }
        };

        // Act
        var result = _sut.CreateUserMessage(submit, sendingPMode);

        // Assert
        Assert.Collection(
            result.MessageProperties,
            p => Assert.Equal(("capability", "info", "receiving"), (p.Name, p.Type, p.Value)),
            p => Assert.Equal(("endpoint", "international"), (p.Name, p.Value)));
    }

    private static Mapped GetMapped(string value, string? pmodeAgreement, string? submitAgreement)
    {
        if (value == pmodeAgreement) return Mapped.PMode;
        if (value == submitAgreement) return Mapped.Submit;
        return Mapped.Default;

    }
}
