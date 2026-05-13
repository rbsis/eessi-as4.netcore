using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Steps;

/// <summary>
/// Composition of Steps
/// </summary>
[NotConfigurable]
public class CompositeStep : IStep
{
    private readonly IList<IStep> _steps;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeStep"/> class. 
    /// Create a <see cref="CompositeStep"/> that acts as a
    /// Composition of <see cref="IStep"/> implementations
    /// </summary>
    /// <param name="steps">
    /// </param>
    public CompositeStep(params IStep[] steps)
    {
        _steps = steps;
    }

    /// <summary>
    /// Send message through the Use Case
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        var messageToSend = messagingContext;
        var result = await StepResult.SuccessAsync(messageToSend);

        foreach (var step in _steps)
        {
            result = await step.ExecuteAsync(messageToSend, cancellation);

            if (result.MessagingContext != null)
            {
                messageToSend = result.MessagingContext;
            }

            if (!result.CanProceed)
            {
                break;
            }
        }

        return result;
    }
}
