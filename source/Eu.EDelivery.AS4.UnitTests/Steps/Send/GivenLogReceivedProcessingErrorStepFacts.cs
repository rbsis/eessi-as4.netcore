using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps.Send;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Repositories;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send;

public class GivenLogReceivedProcessingErrorStepFacts : GivenDatastoreFacts
{
    [Fact]
    public async Task InExceptionGetsInsertedIfErrorResultAndAS4MessageArePresent()
    {
        // Arrange
        string id = Guid.NewGuid().ToString(),
            expected = Guid.NewGuid().ToString();

        var as4Message = AS4Message.Create(new Error($"error-{Guid.NewGuid()}", id));
        var error = new ErrorResult(expected, default);

        // Act
        await ExerciseLog(as4Message, error);

        // Assert
        GetDataStoreContext.AssertInException(id, ex =>
        {
            Assert.NotNull(ex);
            Assert.Equal(expected, ex.Exception);
        });
    }

    [Fact]
    public async Task NoExceptionGetsLoggedIfNoErrorResultIsPresent()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var as4Message = AS4Message.Create(new Error($"error-{Guid.NewGuid()}", id));

        // Act
        await ExerciseLog(as4Message, error: null);

        // Assert
        GetDataStoreContext.AssertInException(id, Assert.Null);
    }

    private async Task ExerciseLog(AS4Message as4Message, ErrorResult? error)
    {
        var sut = new LogReceivedProcessingErrorStep(Default.NewDatastoreRepository(this));

        await sut.ExecuteAsync(
            new MessagingContext(as4Message, MessagingContextMode.Send) { ErrorResult = error }, CancellationToken.None);
    }
}
