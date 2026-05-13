using System.Diagnostics.CodeAnalysis;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.UnitTests.Builders.Entities;
using Eu.EDelivery.AS4.UnitTests.Model;
using Eu.EDelivery.AS4.UnitTests.Common;
using MessageExchangePattern = Eu.EDelivery.AS4.Entities.MessageExchangePattern;

namespace Eu.EDelivery.AS4.UnitTests.Entities;

/// <summary>
/// Testing <see cref="MessageEntity"/>
/// </summary>
public class GivenMessageEntityFacts
{
    public class Create
    {
        [Fact]
        public void HasDefaultOperation()
        {
            Assert.Equal(Operation.NotApplicable, new StubMessageEntity().Operation);
        }

        [Fact]
        public void HasDefaultMessageExchangePattern()
        {
            Assert.Equal(MessageExchangePattern.Push, new StubMessageEntity().MEP);
        }

        [Fact]
        public void HasDefaultMessageType()
        {
            Assert.Equal(MessageType.UserMessage, new StubMessageEntity().EbmsMessageType);
        }

        [Fact]
        public void GetsPartyInfoFromEntity()
        {
            // Arrange
            var expected = CreateAS4MessageWithUserMessage();

            // Act
            var actual = BuildForMessageUnit(expected.FirstUserMessage!);

            // Assert
            MessageEntityAssertion.AssertPartyInfo(expected, actual);
        }

        [Fact]
        public void GetsCollaborationInfo()
        {
            // Arrange
            var expected = CreateAS4MessageWithUserMessage();

            // Act
            var actual = BuildForMessageUnit(expected.FirstUserMessage!);

            // Assert
            MessageEntityAssertion.AssertCollaborationInfo(expected, actual);
        }

        [Fact]
        public void GetsMetaInfoForUserMessage()
        {
            // Arrange
            var expected = CreateAS4MessageWithUserMessage();

            // Act
            var actual = BuildForMessageUnit(expected.FirstUserMessage!);

            // Assert
            MessageEntityAssertion.AssertUserMessageMetaInfo(expected, actual);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetsMetaInfoForSignalMessage(bool isDuplicate)
        {
            // Arrange
            var expected = CreateAS4MessageWithReceiptMessage(isDuplicate: isDuplicate);

            // Act
            var actual = BuildForMessageUnit(expected.FirstSignalMessage!);

            // Assert
            MessageEntityAssertion.AssertSignalMessageMetaInfo(expected, actual);

        }

        [Fact]
        public void GetsSoapEnvelope()
        {
            // Arrange
            var expected = CreateAS4MessageWithUserMessage();

            // Act
            var actual = BuildForMessageUnit(expected.FirstUserMessage!);

            // Assert
            MessageEntityAssertion.AssertSoapEnvelope(expected.FirstUserMessage!, actual);
        }

        private static AS4Message CreateAS4MessageWithUserMessage()
        {
            return AS4Message.Create(new FilledUserMessage());
        }

        private static AS4Message CreateAS4MessageWithReceiptMessage(bool isDuplicate)
        {
            return AS4Message.Create(new FilledNRReceipt { IsDuplicate = isDuplicate });
        }

        private static StubMessageEntity BuildForMessageUnit(MessageUnit expected)
        {
            var message = new StubMessageEntity();
            message.AssignAS4Properties(expected);

            return message;
        }
    }

    public class PMode : GivenMessageEntityFacts
    {
        [Fact]
        public void SendingPModeInformationIsCorrectlySet()
        {
            var entity = new StubMessageEntity();

            var sendingPMode = new SendingProcessingMode() { Id = "sending_pmode_id" };

            entity.SetPModeInformation(sendingPMode);

            Assert.Equal(sendingPMode.Id, entity.PModeId);
            Assert.Equal(entity.PMode, AS4XmlSerializer.ToString(sendingPMode));
        }

        [Fact]
        public void ReceivingPModeInformationIsCorrectlySet()
        {
            var entity = new StubMessageEntity();

            var receivingPMode = new ReceivingProcessingMode() { Id = "sending_pmode_id" };

            entity.SetPModeInformation(receivingPMode);

            Assert.Equal(receivingPMode.Id, entity.PModeId);
            Assert.Equal(entity.PMode, AS4XmlSerializer.ToString(receivingPMode));

        }
    }

    public class Persistence : GivenDatastoreFacts
    {
        [Fact]
        public async Task IdIsCorrectlyRetrieved()
        {
            const string MessageId = "messageId";

            using (var db = GetDataStoreContext())
            {
                var inMessage = new InMessage(MessageId) { MessageLocation = "test" };

                Assert.Equal(default(int), inMessage.Id);

                db.InMessages.Add(inMessage);

                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            using (var db = GetDataStoreContext())
            {
                var inMessage = db.InMessages.FirstOrDefault(m => m.EbmsMessageId == MessageId);
                Assert.NotNull(inMessage);
                Assert.NotEqual(default(int), inMessage.Id);
            }
        }

        [Fact]
        public async Task PModeInformationIsCorrectlyRetrieved()
        {
            const string MessageId = "messageId";
            const string PModeId = "TestPModeId";

            using (var db = GetDataStoreContext())
            {
                var inMessage = new InMessage(MessageId) { MessageLocation = "test" };
                inMessage.SetPModeInformation(new SendingProcessingMode() { Id = PModeId });

                db.InMessages.Add(inMessage);

                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            using (var db = GetDataStoreContext())
            {
                var inMessage = db.InMessages.FirstOrDefault(m => m.EbmsMessageId == MessageId);
                Assert.NotNull(inMessage);
                Assert.False(string.IsNullOrWhiteSpace(inMessage.PModeId));
                Assert.False(string.IsNullOrWhiteSpace(inMessage.PMode));
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private class StubMessageEntity : MessageEntity
    {
        public StubMessageEntity() : base(Guid.NewGuid().ToString())
        {
        }

        /// <summary>
        /// Gets the sending processing mode based on a child representation of a message entity.
        /// </summary>
        public override SendingProcessingMode? GetSendingPMode()
        {
            return null;
        }

        /// <summary>
        /// Gets the receiving processing mode based on a child representation of a message entity.
        /// </summary>
        public override ReceivingProcessingMode? GetReceivingPMode()
        {
            return null;
        }
    }
}
