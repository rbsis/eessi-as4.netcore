using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Steps;

namespace AS4.ParserService.Infrastructure;

public class StepProcessor
{
    private readonly IStepBuilder _stepBuilder;

    public StepProcessor(IStepBuilder stepBuilder)
    {
        _stepBuilder = stepBuilder;
    }

    internal async Task<MessagingContext> ExecuteStepsAsync(
        MessagingContext context,
        StepConfiguration stepConfig,
        CancellationToken cancellation)
    {
        try
        {
            var steps = CreateSteps(stepConfig.NormalPipeline);
            var result = await ExecuteStepsAsync(steps, context, cancellation);

            var weHaveAnyUnhappyPath = stepConfig.ErrorPipeline != null;
            if (!result.Succeeded && weHaveAnyUnhappyPath && result.MessagingContext.Exception == null)
            {
                var unhappySteps = CreateSteps(stepConfig.ErrorPipeline);
                result = await ExecuteStepsAsync(unhappySteps, result.MessagingContext, cancellation);
            }

            return result.MessagingContext;
        }
        catch (Exception ex)
        {
            return new MessagingContext(ex);
        }
    }

    private IEnumerable<IStep> CreateSteps(Step[]? pipeline) => pipeline != null ? _stepBuilder.BuildSteps(pipeline) : [];

    private static async Task<StepResult> ExecuteStepsAsync(
        IEnumerable<IStep> steps,
        MessagingContext context,
        CancellationToken cancellation)
    {
        var result = await StepResult.SuccessAsync(context);

        var currentContext = context;

        foreach (var step in steps)
        {
            result = await step.ExecuteAsync(currentContext, cancellation);

            if (!result.CanProceed || !result.Succeeded || result.MessagingContext?.Exception != null)
            {
                return result;
            }

            if (result.MessagingContext != null && currentContext != result.MessagingContext)
            {
                currentContext = result.MessagingContext;
            }
        }

        return result;
    }
}
