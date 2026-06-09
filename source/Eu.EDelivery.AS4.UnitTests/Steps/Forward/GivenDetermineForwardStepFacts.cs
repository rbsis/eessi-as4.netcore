using System.Configuration;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps.Forward;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Forward;

public class GivenDetermineForwardStepFacts
{
    public class ValidMessagingContextFacts
    {
        [Fact]
        public async Task SendingPModeCorrectlyDetermined()
        {
            const string SendingPModeId = "Forward_SendingPMode_Id";

            var receivingPMode = new ReceivingProcessingMode()
            {
                MessageHandling = new MessageHandling()
                {
                    Item = new AS4.Model.PMode.Forward()
                    {
                        SendingPMode = SendingPModeId
                    }
                }
            };

            var config = new StubConfig(
                sendingPModes: new Dictionary<string, SendingProcessingMode>()
                {
                    [SendingPModeId] = new SendingProcessingMode() { Id = SendingPModeId }
                },
                receivingPModes: new Dictionary<string, ReceivingProcessingMode>());

            var context = new MessagingContext(new ReceivedMessage(Stream.Null), MessagingContextMode.Forward)
            {
                ReceivingPMode = receivingPMode
            };

            var sut = new DetermineRoutingStep(NullLogger<DetermineRoutingStep>.Instance, config);
            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.NotNull(result.MessagingContext.SendingPMode);
        }
    }

    public class InvalidMessagingContextFacts
    {
        [Fact]
        public async Task ExceptionWhenNoReceivingPModeAvailable()
        {
            var messagingContext = new MessagingContext(new ReceivedMessage(Stream.Null), MessagingContextMode.Forward) { ReceivingPMode = null };

            var step = new DetermineRoutingStep(NullLogger<DetermineRoutingStep>.Instance, StubConfig.Default);

            await Assert.ThrowsAsync<InvalidOperationException>(() => step.ExecuteAsync(messagingContext, CancellationToken.None));
        }


        [Fact]
        public async Task ExceptionWhenReceivingPModeIsInvalid()
        {
            var receivingPMode = new ReceivingProcessingMode()
            {
                MessageHandling = new MessageHandling()
                {
                    Item = new AS4.Model.PMode.Deliver()
                }
            };

            var messagingContext = new MessagingContext(new ReceivedMessage(Stream.Null), MessagingContextMode.Forward)
            {
                ReceivingPMode = receivingPMode
            };

            var step = new DetermineRoutingStep(NullLogger<DetermineRoutingStep>.Instance, StubConfig.Default);

            await Assert.ThrowsAsync<ConfigurationErrorsException>(() => step.ExecuteAsync(messagingContext, CancellationToken.None));
        }

        [Fact]
        public async Task ExceptionWhenSendingPModeNotFound()
        {
            var receivingPMode = new ReceivingProcessingMode
            {
                MessageHandling = new MessageHandling
                {
                    Item = new AS4.Model.PMode.Forward
                    {
                        SendingPMode = "Forward_SendingPMode_Id"
                    }
                }
            };

            var messagingContext = new MessagingContext(new ReceivedMessage(Stream.Null), MessagingContextMode.Forward)
            {
                ReceivingPMode = receivingPMode
            };

            var step = new DetermineRoutingStep(NullLogger<DetermineRoutingStep>.Instance, StubConfig.Default);

            await Assert.ThrowsAsync<ConfigurationErrorsException>(() => step.ExecuteAsync(messagingContext, CancellationToken.None));
        }
    }
}

