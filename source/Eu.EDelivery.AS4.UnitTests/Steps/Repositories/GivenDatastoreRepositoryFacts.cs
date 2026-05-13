using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Repositories;

/// <summary>
/// Testing the <see cref="DatastoreRepository" />
/// </summary>
public class GivenDatastoreRepositoryFacts : GivenDatastoreFacts
{
    public class OutMessages : GivenDatastoreRepositoryFacts
    {
        [Fact]
        public void ThenGetOutMessageSucceeded()
        {
            // Arrange
            const string EbmsMessageId = "message-id";
            const Operation Expected = Operation.Delivered;
            InsertOutMessage(EbmsMessageId, Expected);

            var repository = new DatastoreRepository(NullLogger<DatastoreRepository>.Instance, this);

            // Act
            var actual =
                repository.GetOutMessageData(@where: m => m.EbmsMessageId == EbmsMessageId,
                                             selection: m => m.Operation)
                          .SingleOrDefault();

            // Assert
            Assert.Equal(Expected, actual);
        }

        [Fact]
        public void ThenInsertOutMessageSucceeds()
        {
            // Arrange
            var outMessage = new OutMessage(ebmsMessageId: "message-id") { MessageLocation = "location" };

            // Act
            new DatastoreRepository(NullLogger<DatastoreRepository>.Instance, this).InsertOutMessage(outMessage);

            // Assert
            AssertOutMessage(outMessage.EbmsMessageId, Assert.NotNull);
        }

        [Fact]
        public void ThenUpdateOutMessageSucceeds()
        {
            // Arrange
            const string SharedId = "message-id";
            var outMessageId = InsertOutMessage(SharedId, Operation.ToBeSent).Id;

            // Act
            new DatastoreRepository(NullLogger<DatastoreRepository>.Instance, this).UpdateOutMessage(
               outMessageId,
               m => m.Operation = Operation.Sent);

            // Assert
            AssertOutMessage(SharedId, m => Assert.Equal(Operation.Sent, m?.Operation));
        }

        private OutMessage InsertOutMessage(string ebmsMessageId, Operation operation = Operation.NotApplicable)
        {
            var outMessage = new OutMessage(ebmsMessageId: ebmsMessageId)
            {
                Operation = operation
            };

            GetDataStoreContext.InsertOutMessage(outMessage, withReceptionAwareness: false);

            return outMessage;
        }

        private void AssertOutMessage(string messageId, Action<OutMessage?> assertAction)
        {
            using var contex = GetDataStoreContext();
            var outMessage = contex.OutMessages.FirstOrDefault(m => m.EbmsMessageId.Equals(messageId));
            assertAction(outMessage);
        }
    }

    public class InExceptions : GivenDatastoreRepositoryFacts
    {
        [Fact]
        public void ThenInsertInExceptionSucceeds()
        {
            // Arrange
            var inException = InException.ForEbmsMessageId($"inex-{Guid.NewGuid()}", "error");

            // Act
            new DatastoreRepository(NullLogger<DatastoreRepository>.Instance, this).InsertInException(inException);

            GetDataStoreContext.AssertInException(inException.EbmsRefToMessageId!, Assert.NotNull);
        }
    }

    public class OutExceptions : GivenDatastoreRepositoryFacts
    {
        [Fact]
        public void ThenInsertOutExceptionSucceeds()
        {
            // Arrange
            var outException = OutException.ForEbmsMessageId($"outex-{Guid.NewGuid()}", "error");

            // Act
            new DatastoreRepository(NullLogger<DatastoreRepository>.Instance, this).InsertOutException(outException);

            // Assert
            GetDataStoreContext.AssertOutException(outException.EbmsRefToMessageId!, Assert.NotNull);
        }
    }

    public class InMessages : GivenDatastoreRepositoryFacts
    {
        [Fact]
        public void SelectsOnlyMessageLocation()
        {
            // Arrange
            const string MessageId = "single-id";
            const string Expected = "message-location";
            InsertInMessage(MessageId, m => m.MessageLocation = Expected);

            // Act
            var actual = ExerciseRepository(sut => sut.GetInMessageData(MessageId, m => m.MessageLocation));

            // Assert
            Assert.Equal(Expected, actual);
        }

        [Fact]
        public void GetsMessageIdsForFoundUserMessages()
        {
            TestFoundInMessagesFor(id => InsertInMessageWithOperation(id), repository => repository.SelectExistingInMessageIds);
        }

        [Fact]
        public void GetsMmessagesIdsForFoundSignalMessages()
        {
            TestFoundInMessagesFor(InsertRefInMessage, repository => repository.SelectExistingInRefToMessageIds);
        }

        private void TestFoundInMessagesFor(Action<string> insertion, Func<DatastoreRepository, Func<IEnumerable<string>, IEnumerable<string>>> sutAction)
        {
            // Arrange
            const string ExpectedId = "message-id";
            insertion(ExpectedId);

            var repository = new DatastoreRepository(NullLogger<DatastoreRepository>.Instance, this);
            var expectedMessageIds = new[] { ExpectedId };

            // Act
            var actualMessageIds = sutAction(repository)(expectedMessageIds);

            // Assert
            Assert.Equal(expectedMessageIds, actualMessageIds);
        }

        [Theory]
        [InlineData("shared-id")]
        public void ThenInMessageExistsSucceeded(string sharedId)
        {
            // Arrange
            InsertInMessageWithOperation(sharedId);

            var repository = new DatastoreRepository(NullLogger<DatastoreRepository>.Instance, this);

            // Act
            var result = repository.InMessageExists(m => m.EbmsMessageId == sharedId);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("shared-id")]
        public void ThenInsertInMessageSucceeds(string sharedId)
        {
            // Arrange
            InsertInMessageWithOperation(sharedId);

            // Assert
            GetDataStoreContext.AssertInMessage(sharedId, Assert.NotNull);
        }

        [Theory]
        [InlineData("share-id")]
        public void ThenUpdateInMessageSucceeds(string sharedId)
        {
            // Arrange
            InsertInMessageWithOperation(sharedId, Operation.ToBeDelivered);

            // Act
            new DatastoreRepository(NullLogger<DatastoreRepository>.Instance, this).UpdateInMessage(
                sharedId,
                m => m.Operation = Operation.Delivered);

            // Assert
            GetDataStoreContext.AssertInMessage(sharedId, m =>
            {
                Assert.NotNull(m);
                Assert.Equal(Operation.Delivered, m.Operation);
            });
        }

        private void InsertInMessageWithOperation(string ebmsMessageId, Operation operation = Operation.NotApplicable)
        {
            InsertInMessage(ebmsMessageId, m => m.Operation = operation);
        }

        private void InsertInMessage(string messageId, Action<InMessage> arrangeMessage)
        {
            var message = new InMessage(ebmsMessageId: messageId);
            arrangeMessage(message);

            GetDataStoreContext.InsertInMessage(message);
        }

        private void InsertRefInMessage(string refToEbmsMessageId)
        {
            GetDataStoreContext.InsertInMessage(new InMessage(Guid.NewGuid().ToString()) { EbmsRefToMessageId = refToEbmsMessageId });
        }

        private TResult ExerciseRepository<TResult>(Func<DatastoreRepository, TResult> act)
        {
            using var context = GetDataStoreContext();
            var sut = new DatastoreRepository(NullLogger<DatastoreRepository>.Instance, this);

            // Act
            return act(sut);
        }
    }
}
