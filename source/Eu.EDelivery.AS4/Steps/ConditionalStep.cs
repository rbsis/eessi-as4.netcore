using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Steps;

/// <summary>
/// <see cref="IStep" />
/// </summary>
[NotConfigurable]
public class ConditionalStep : IStep
{
    private readonly Func<MessagingContext, bool> _condition;
    private readonly Step[] _thenSteps;
    private readonly Step[] _elseSteps;
    private readonly IStepBuilder _stepBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionalStep" /> class.
    /// </summary>
    /// <param name="condition"></param>
    /// <param name="thenStepConfig"></param>
    /// <param name="elseStepConfig"></param>
    /// <param name="stepBuilder"></param>
    public ConditionalStep(
        Func<MessagingContext, bool> condition,
        Step[] thenStepConfig,
        Step[] elseStepConfig,
        IStepBuilder stepBuilder)
    {
        _condition = condition;
        _thenSteps = thenStepConfig;
        _elseSteps = elseStepConfig;
        _stepBuilder = stepBuilder;
    }

    /// <summary>
    /// Run the selected step
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (_condition(messagingContext))
        {
            var steps = _stepBuilder.BuildAsSingleStep(_thenSteps);
            return await steps.ExecuteAsync(messagingContext, cancellation);
        }
        else
        {
            var steps = _stepBuilder.BuildAsSingleStep(_elseSteps);
            return await steps.ExecuteAsync(messagingContext, cancellation);
        }
    }
}
