using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps;

namespace Eu.EDelivery.AS4.UnitTests.Steps;

internal class DummyStep : IStep
{
    /// <summary>
    /// Execute the step for a given <paramref name="internalMessage"/>.
    /// </summary>
    /// <param name="internalMessage">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public Task<StepResult> ExecuteAsync(MessagingContext internalMessage, CancellationToken cancellation)
    {
        throw new NotImplementedException();
    }
}