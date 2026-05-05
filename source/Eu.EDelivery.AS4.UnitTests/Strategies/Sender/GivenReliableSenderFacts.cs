using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Strategies.Sender;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Sender;

/// <summary>
/// Testing <see cref="ReliableSender" />
/// </summary>
public class GivenReliableSenderFacts
{
    private static readonly ILogger<ReliableDeliverSender> _reliableDeliverSenderLogger = NullLogger<ReliableDeliverSender>.Instance;
    private static readonly ILogger<ReliableNotifySender> _reliableNotifySenderLogger = NullLogger<ReliableNotifySender>.Instance;

    public class Configure
    {
        [Fact]
        public void SenderDelegatesConfigurationIfDeliverSender()
        {
            // Arrange
            var stubSender = new SpySender();
            var sut = new ReliableDeliverSender(_reliableDeliverSenderLogger, stubSender);

            // Act
            sut.Configure(new AS4.Model.PMode.Method());

            // Assert
            Assert.True(stubSender.IsConfigured);
        }

        [Fact]
        public void SenderDelegatesConfigurationIfNotifySender()
        {
            // Arrange
            var stubSender = new SpySender();
            var sut = new ReliableNotifySender(_reliableNotifySenderLogger, stubSender);

            // Act
            sut.Configure(new AS4.Model.PMode.Method());

            // Assert
            Assert.True(stubSender.IsConfigured);
        }
    }

    public class Send
    {
        [Fact]
        public async Task SenderCatchesAndRetrowsAS4ExceptionIfDeliverMessage()
        {
            // Arrange
            var sut = new ReliableDeliverSender(_reliableDeliverSenderLogger, new SaboteurSender());

            // Act
            var r = await sut.SendAsync(DummyDeliverMessage(), CancellationToken.None);

            // Assert
            Assert.Equal(SendResult.FatalFail, r);
        }

        private static DeliverMessageEnvelope DummyDeliverMessage()
        {
            return new DeliverMessageEnvelope(new DeliverMessage(), "", []);
        }

        [Fact]
        public async Task SenderCatchesAndRethrowsAS4ExceptionIfNotifyMesage()
        {
            // Arrange
            var sut = new ReliableNotifySender(_reliableNotifySenderLogger, new SaboteurSender());

            // Act
            var r = await sut.SendAsync(DummyNotifyMessage(), CancellationToken.None);

            // Assert
            Assert.Equal(SendResult.FatalFail, r);
        }

        private static NotifyMessageEnvelope DummyNotifyMessage()
        {
            return new NotifyMessageEnvelope(new MessageInfo(), default, [], "", typeof(InMessage));
        }
    }
}
