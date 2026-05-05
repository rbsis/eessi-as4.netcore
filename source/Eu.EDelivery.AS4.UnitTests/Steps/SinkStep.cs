using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps;

namespace Eu.EDelivery.AS4.UnitTests.Steps;

/// <summary>
/// <see cref="IStep"/> implementation to 'sink' the request.
/// </summary>
internal class SinkStep : IStep
{
    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext"/>.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        return await StepResult.SuccessAsync(messagingContext);
    }
}
