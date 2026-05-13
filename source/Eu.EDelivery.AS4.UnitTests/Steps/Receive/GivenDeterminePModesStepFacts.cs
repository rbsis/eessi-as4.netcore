using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AgreementReference = Eu.EDelivery.AS4.Model.Core.AgreementReference;
using Party = Eu.EDelivery.AS4.Model.PMode.Party;
using PartyId = Eu.EDelivery.AS4.Model.PMode.PartyId;
using ReceivePMode = Eu.EDelivery.AS4.Model.PMode.ReceivingProcessingMode;
using Service = Eu.EDelivery.AS4.Model.Core.Service;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Receive;

/// <summary>
/// Testing the <see cref="DeterminePModesStep" />
/// </summary>
public class GivenDeterminePModesStepFacts : GivenDatastoreFacts
{
    private readonly Mock<IConfig> _mockedConfig;
    private readonly DeterminePModesStep _step;

    public GivenDeterminePModesStepFacts()
    {
        _mockedConfig = new Mock<IConfig>();
        _step = new DeterminePModesStep(
            NullLogger<DeterminePModesStep>.Instance,
            _mockedConfig.Object,
            Default.NewDatastoreRepository(this),
            Default.PModeRuleEngine);
    }

    public class GivenValidArguments : GivenDeterminePModesStepFacts
    {
        [Fact]
        public async Task DetermineBothSendingAndReceivingPModeWhenBundled()
        {
            // Arrange
            var nonMultihopSignal = new Receipt($"receipt-{Guid.NewGuid()}", $"reftoid-{Guid.NewGuid()}");

            var receivePModeId = $"receive-pmodeid-{Guid.NewGuid()}";
            var userMesssage = new UserMessage(
                messageId: $"user-{Guid.NewGuid()}",
                collaboration: new AS4.Model.Core.CollaborationInfo(
                    new AgreementReference(
                        "agreement",
                        receivePModeId),
                    Service.TestService,
                    Constants.Namespaces.TestAction,
                    AS4.Model.Core.CollaborationInfo.DefaultConversationId));

            var sendPModeId = $"send-pmodeid-{Guid.NewGuid()}";
            var expected = new SendingProcessingMode { Id = sendPModeId };
            InsertOutMessage(nonMultihopSignal.RefToMessageId!, expected);

            var msg = AS4Message.Create(userMesssage);
            msg.AddMessageUnit(nonMultihopSignal);

            // Act
            var result = await ExerciseDeterminePModes(
                msg,
                new ReceivePMode
                {
                    Id = receivePModeId,
                });

            // Assert
            Assert.NotNull(result.MessagingContext.ReceivingPMode);
            Assert.NotNull(result.MessagingContext.SendingPMode);
            Assert.Equal(receivePModeId, result.MessagingContext.ReceivingPMode.Id);
            Assert.Equal(sendPModeId, result.MessagingContext.SendingPMode.Id);
        }

        [Fact]
        public async Task DontUseScoringSystemReceivingPModeWhenAlreadyConfigure()
        {
            // Arrange
            var expected = new ReceivePMode { Id = "static-receive-configured" };

            // Act
            var result = await _step.ExecuteAsync(
                new MessagingContext(
                    AS4Message.Empty,
                    MessagingContextMode.Receive)
                {
                    ReceivingPMode = expected
                }, CancellationToken.None);

            // Assert
            Assert.Same(expected, result.MessagingContext.ReceivingPMode);
        }

        [Fact]
        public async Task SendingPModeIsFoundIfSignalMessage()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var expected = new SendingProcessingMode { Id = Guid.NewGuid().ToString() };
            InsertOutMessage(messageId, expected);

            var as4Message = AS4Message.Create(new Receipt($"receipt-{Guid.NewGuid()}", messageId));

            // Act
            var result = await ExerciseDeterminePModes(as4Message);

            // Assert
            Assert.NotNull(result.MessagingContext.SendingPMode);
            Assert.Equal(expected.Id, result.MessagingContext.SendingPMode.Id);
        }

        private void InsertOutMessage(string messageId, SendingProcessingMode pmode)
        {
            var outMessage = new OutMessage(ebmsMessageId: messageId);
            outMessage.SetPModeInformation(pmode);

            GetDataStoreContext.InsertOutMessage(outMessage);
        }

        private async Task<StepResult> ExerciseDeterminePModes(AS4Message message, params ReceivePMode[] pmodes)
        {
            var stubConfig = new Mock<IConfig>();
            stubConfig.Setup(c => c.GetReceivingPModes()).Returns(pmodes);
            var sut = new DeterminePModesStep(
                NullLogger<DeterminePModesStep>.Instance,
                stubConfig.Object,
                Default.NewDatastoreRepository(this),
                Default.PModeRuleEngine);

            return await sut.ExecuteAsync(
                new MessagingContext(message, MessagingContextMode.Receive), CancellationToken.None);
        }
    }

    /// <summary>
    /// Testing the step with invalid arguments
    /// </summary>
    public class GivenInvalidArguments : GivenDeterminePModesStepFacts
    {
        [Theory]
        [InlineData("action", "service")]
        public async Task ThenServiceAndActionIsNotEnoughAsync(string action, string service)
        {
            // Arrange
            ArrangePModeThenServiceAndActionIsNotEnough(action, service);

            var userMessage = new UserMessage(
                Guid.NewGuid().ToString(),
                new AS4.Model.Core.CollaborationInfo(
                    Maybe<AgreementReference>.Nothing,
                    new Service(service),
                    action,
                    "1"));

            var messagingContext = new MessagingContext(AS4Message.Create(userMessage), MessagingContextMode.Receive);

            // Act
            var result = await _step.ExecuteAsync(messagingContext, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded);
            Assert.NotNull(result.MessagingContext.ErrorResult);
            Assert.Equal(ErrorCode.Ebms0010, result.MessagingContext.ErrorResult.Code);
        }

        private void ArrangePModeThenServiceAndActionIsNotEnough(string action, string service)
        {
            var pmode = CreatePModeWithActionService(service, action);
            pmode.MessagePackaging.CollaborationInfo!.AgreementReference.Value = "not-equal";
            DifferentiatePartyInfo(pmode);
            SetupPModes(pmode, new ReceivePMode() { Id = "other id" });
        }

        [Theory]
        [InlineData("name", "type")]
        public async Task ThenAgreementRefIsNotEnoughAsync(string name, string type)
        {
            // Arrange
            var agreementRef = new AS4.Model.PMode.AgreementReference { Value = name, Type = type, PModeId = "pmode-id" };
            ArrangePModeThenAgreementRefIsNotEnough(agreementRef);

            var userMessage = new UserMessage(
                Guid.NewGuid().ToString(),
                new AS4.Model.Core.CollaborationInfo(
                    agreement: new AgreementReference(name, type, "pmode-id"),
                    service: new Service("service"),
                    action: "action",
                    conversationId: "1"));

            var messagingContext = new MessagingContext(AS4Message.Create(userMessage), MessagingContextMode.Receive);

            // Act
            var result = await _step.ExecuteAsync(messagingContext, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded);
            Assert.NotNull(result.MessagingContext.ErrorResult);
            Assert.Equal(ErrorCode.Ebms0010, result.MessagingContext.ErrorResult.Code);
        }

        private void ArrangePModeThenAgreementRefIsNotEnough(AS4.Model.PMode.AgreementReference agreementRef)
        {
            var pmode = CreatePModeWithAgreementRef(agreementRef);
            DifferentiatePartyInfo(pmode);
            SetupPModes(pmode, new ReceivePMode());
        }
        private static void DifferentiatePartyInfo(ReceivePMode pmode)
        {
            const string FromId = "from-Id";
            const string ToId = "to-Id";

            var fromParty = new Party { Role = FromId, PartyIds = [new PartyId { Id = FromId }] };
            var toParty = new Party { Role = ToId, PartyIds = [new PartyId { Id = ToId }] };

            pmode.MessagePackaging.PartyInfo = new PartyInfo { FromParty = fromParty, ToParty = toParty };
        }
    }

    protected static ReceivePMode CreateDefaultPMode(string id) => new()
    {
        Id = id,
        MessagePackaging =
        {
            CollaborationInfo = new(),
            PartyInfo = new()
        },
    };

    protected void SetupPModes(params ReceivePMode[] pmodes)
    {
        _mockedConfig.Setup(c => c.GetReceivingPModes()).Returns(pmodes);
    }

    protected static ReceivePMode CreatePModeWithAgreementRef(AS4.Model.PMode.AgreementReference agreementRef)
    {
        var pmode = CreateDefaultPMode("defaultPMode");
        pmode.MessagePackaging.CollaborationInfo!.AgreementReference = agreementRef;

        return pmode;
    }

    protected static ReceivePMode CreatePModeWithActionService(string service, string action)
    {
        var pmode = CreateDefaultPMode("defaultPMode");
        pmode.MessagePackaging.CollaborationInfo!.Action = action;
        pmode.MessagePackaging.CollaborationInfo!.Service.Value = service;

        return pmode;
    }

    protected static void AssertPMode(ReceivePMode expectedPMode, StepResult result)
    {
        Assert.Equal(expectedPMode, result.MessagingContext.ReceivingPMode);
    }

}
