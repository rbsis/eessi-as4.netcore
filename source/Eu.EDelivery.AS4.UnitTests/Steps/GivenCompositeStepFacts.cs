using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.UnitTests.Model;
using Moq;

namespace Eu.EDelivery.AS4.UnitTests.Steps;

/// <summary>
/// Testing <seealso cref="CompositeStep" />
/// </summary>
public class GivenCompositeStepFacts
{
    /// <summary>
    /// Testing if the transmitter succeeds
    /// </summary>
    public class GivenCompositeStepSucceeds : GivenCompositeStepFacts
    {
        [Fact]
        public async Task ThenTransmitMessageSucceeds()
        {
            // Arrange
            var dummyMessage = CreateDummyMessageWithAttachment();
            var expectedStepResult = await StepResult.SuccessAsync(dummyMessage);

            var compositeStep = new CompositeStep(CreateMockStepWith(expectedStepResult).Object);

            // Act
            var actualStepResult = await compositeStep.ExecuteAsync(dummyMessage, CancellationToken.None);

            // Assert
            Assert.Equal(expectedStepResult.MessagingContext, actualStepResult.MessagingContext);
        }

        [Fact]
        public async Task ThenStepStopExecutionWithMarkedStepResult()
        {
            // Arrange
            var expectedMessage = CreateDummyMessageWithAttachment();
            var stopExecutionResult = (await StepResult.SuccessAsync(expectedMessage)).AndStopExecution();

            var spyStep = new SpyStep();
            var compositeStep = new CompositeStep(CreateMockStepWith(stopExecutionResult).Object, spyStep);

            // Act
            var actualResult = await compositeStep.ExecuteAsync(new EmptyMessagingContext(), CancellationToken.None);

            // Assert  
            Assert.False(spyStep.IsCalled);
            Assert.Equal(expectedMessage, actualResult.MessagingContext);
        }

        private static MessagingContext CreateDummyMessageWithAttachment()
        {

            var message = AS4Message.Empty;
            message.AddAttachment(new Attachment(Stream.Null, "text/plain"));

            return new MessagingContext(message, MessagingContextMode.Unknown);
        }

        private static Mock<IStep> CreateMockStepWith(StepResult stepResult)
        {
            var mockStep = new Mock<IStep>();

            mockStep.Setup(m => m.ExecuteAsync(It.IsAny<MessagingContext>(), CancellationToken.None))
                    .ReturnsAsync(stepResult);

            return mockStep;
        }
    }
}
