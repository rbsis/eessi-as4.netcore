using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps;

namespace Eu.EDelivery.AS4.UnitTests.Steps;

/// <summary>
/// <see cref="IStep"/> implementation to "spy" on the step execution.
/// </summary>
internal class SpyStep : IStep
{
    /// <summary>
    /// Gets a value indicating whether the step is executed.
    /// </summary>
    public bool IsCalled { get; private set; }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext"/>.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        IsCalled = true;
        return StepResult.SuccessAsync(messagingContext);
    }

}

public class SpyStepFacts
{
    [Fact]
    public async Task TestSpyStep()
    {
        // Arrange
        var step = new SpyStep();

        // Act
        await step.ExecuteAsync(messagingContext: new MessagingContext(AS4Message.Empty, MessagingContextMode.Send), cancellation: CancellationToken.None);

        // Assert
        Assert.True(step.IsCalled);
    }
}
