using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Eu.EDelivery.AS4.UnitTests.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Receivers;

/// <summary>
/// Testing <see cref="PullRequestReceiver"/>
/// </summary>
public class GivenPullRequestReceiverFacts
{
    public class Configure
    {
        [Fact]
        public void FailsWithMissingIntervalAttributes()
        {
            // Arrange
            var receiver = new PullRequestReceiver(NullLogger<PullRequestReceiver>.Instance, StubConfig.Default);
            var invalidReceiverSetting = new Setting("01-send", value: string.Empty);

            // Act
            receiver.Configure([invalidReceiverSetting]);

            // Assert
            AssertOnMessageReceived(receiver);
        }

        [Fact]
        public void FailsWithMissingSendingPMode()
        {
            // Arrange
            var receiver = new PullRequestReceiver(NullLogger<PullRequestReceiver>.Instance, StubConfig.Default);
            var receiverSetting = new Setting("unknown-pmode", string.Empty);

            // Act
            receiver.Configure([receiverSetting]);

            // Assert
            AssertOnMessageReceived(receiver);
        }

        private static void AssertOnMessageReceived(IReceiver receiver)
        {
            var waitHandle = new ManualResetEvent(initialState: false);
            receiver.StartReceiving(SetEvent(waitHandle), CancellationToken.None);

            Assert.False(waitHandle.WaitOne(TimeSpan.FromMilliseconds(500)));
        }

        [Fact]
        public async Task TestSetEvent()
        {
            // Arrange
            var waitHandle = new ManualResetEvent(initialState: false);
            var func = SetEvent(waitHandle);

            // Act
            await func(null, CancellationToken.None);

            // Assert
            Assert.True(waitHandle.WaitOne());
        }

        private static Func<ReceivedMessage?, CancellationToken, Task<MessagingContext>> SetEvent(EventWaitHandle waitHandle)
        {
            return (message, token) =>
            {
                waitHandle.Set();
                return Task.FromResult((MessagingContext)new EmptyMessagingContext());
            };
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3881:\"IDisposable\" should be implemented correctly", Justification = "<Pending>")]
    public class StartReceiving : IDisposable
    {
        private readonly ManualResetEvent _waitHandle;
        private readonly Seriewatch _seriewatch;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartReceiving"/> class.
        /// </summary>
        public StartReceiving()
        {
            _waitHandle = new ManualResetEvent(initialState: false);
            _seriewatch = new Seriewatch();
        }

        [Fact]
        public void StartReceiver()
        {
            var stubConfig = new StubConfig(
                sendingPModes: new Dictionary<string, SendingProcessingMode>
                {
                    ["01-send"] = AS4XmlSerializer.FromString<SendingProcessingMode>(Properties.Resources.send_01)!
                },
                receivingPModes: new Dictionary<string, ReceivingProcessingMode>
                {
                    ["01-receive"] = AS4XmlSerializer.FromString<ReceivingProcessingMode>(Properties.Resources.receive_01)!
                });

            // Arrange
            var receiver = new PullRequestReceiver(NullLogger<PullRequestReceiver>.Instance, stubConfig);
            var receiverSetting = CreateMockReceiverSetting();
            receiver.Configure([receiverSetting]);

            // Act
            receiver.StartReceiving(OnMessageReceived, CancellationToken.None);

            // Assert
            Assert.True(_waitHandle.WaitOne(timeout: TimeSpan.FromMinutes(2)));
        }

        private static Setting CreateMockReceiverSetting()
        {
            var minTimeAttribute = new StubXmlAttribute("tmin", "0:00:01");
            var maxTimeAttribute = new StubXmlAttribute("tmax", "0:00:25");

            return new Setting("01-send", string.Empty)
            {
                Attributes = [minTimeAttribute, maxTimeAttribute]
            };
        }

        private async Task<MessagingContext> OnMessageReceived(
            ReceivedMessage receivedMessage,
            CancellationToken cancellationToken)
        {
            var actualPMode = await AS4XmlSerializer.FromStreamAsync<SendingProcessingMode>(receivedMessage.UnderlyingStream, CancellationToken.None);
            Assert.Equal("01-pmode", actualPMode!.Id);

            if (_seriewatch.TrackSerie(maxSerieCount: 3))
            {
                Assert.True(_seriewatch.GetSerie(1) > _seriewatch.GetSerie(0));
                _waitHandle.Set();
            }

            return new EmptyMessagingContext();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _waitHandle?.Dispose();
        }
    }
}
