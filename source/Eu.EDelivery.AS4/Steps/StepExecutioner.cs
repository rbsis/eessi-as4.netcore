using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services.Journal;

namespace Eu.EDelivery.AS4.Steps;

public class StepExecutioner : IStepExecutioner
{
    private readonly IEnumerable<IStep> _normalPipeline;
    private readonly IEnumerable<IStep> _errorPipeline;
    private readonly IAgentExceptionHandler _exceptionHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="StepExecutioner"/> class.
    /// </summary>
    /// <param name="normalPipeline">The configuration used to build <see cref="IStep"/> instances.</param>
    /// <param name="errorPipeline"></param>
    /// <param name="handler">The handler used to handle exceptions during the step executions.</param>
    public StepExecutioner(
        IEnumerable<IStep> normalPipeline,
        IEnumerable<IStep> errorPipeline,
        IAgentExceptionHandler handler)
    {
        _normalPipeline = normalPipeline;
        _errorPipeline = errorPipeline;
        _exceptionHandler = handler;
    }

    /// <summary>
    /// Run through all the configured steps using the given <paramref name="currentContext"/> as input.
    /// </summary>
    /// <param name="currentContext">The input that gets passed to the step pipeline.</param>
    /// <returns>The result of the last-executed step from the normal or error pipeline if there hasn't been an exception occured.</returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteStepsAsync(MessagingContext currentContext, CancellationToken cancellation)
    {
        if (!_normalPipeline.Any())
        {
            return await StepResult.SuccessAsync(currentContext);
        }

        var result = await StepResult.SuccessAsync(currentContext);

        try
        {
            result = await ExecuteStepsAsync(_normalPipeline, result, cancellation);
        }
        catch (Exception exception)
        {
            var handled =
                await _exceptionHandler.HandleExecutionExceptionAsync(exception, currentContext, cancellation);

            return await StepResult.FailedAsync(handled);
        }

        try
        {
            if (!result.Succeeded && _errorPipeline.Any() && result.MessagingContext.Exception == null)
            {
                result = await ExecuteStepsAsync(_errorPipeline, result, cancellation);
            }

            return result;
        }
        catch (Exception exception)
        {
            var handled = await _exceptionHandler.HandleErrorExceptionAsync(exception, result.MessagingContext, cancellation);

            return await StepResult.FailedAsync(handled);
        }
    }

    private static async Task<StepResult> ExecuteStepsAsync(
        IEnumerable<IStep> steps,
        StepResult initialResult,
        CancellationToken cancellation)
    {
        var lastResult = initialResult;
        var currentContext = lastResult.MessagingContext;
        var journals = lastResult.Journal.ToList();

        foreach (var step in steps)
        {
            var nextResult = await ExecuteStepAsync(currentContext, step, cancellation);

            AddOrUpdateJournal(journals, nextResult);

            if (!nextResult.CanProceed || !nextResult.Succeeded || nextResult.MessagingContext.Exception != null)
            {
                return nextResult.WithJournal(journals);
            }

            if (nextResult.MessagingContext != null && currentContext != nextResult.MessagingContext)
            {
                currentContext = nextResult.MessagingContext;
            }

            lastResult = nextResult;
        }

        return lastResult.WithJournal(journals);
    }

    private static void AddOrUpdateJournal(ICollection<JournalLogEntry> journals, StepResult nextResult)
    {
        foreach (var entry in nextResult.Journal)
        {
            var existed = journals.FirstOrDefault(j => JournalLogEntryComparer.ByEbmsMessageId.Equals(j, entry));

            if (existed != null)
            {
                existed.AddLogEntries(entry.LogEntries);
            }
            else
            {
                journals.Add(entry);
            }
        }
    }

    private static async Task<StepResult> ExecuteStepAsync(MessagingContext currentContext, IStep step, CancellationToken cancellation)
    {
        var executeAsync = step.ExecuteAsync(currentContext, cancellation) ?? throw new InvalidOperationException(
                $"Asynchronous result of step: {step.GetType().Name} returns 'null'");
        var nextResult = await executeAsync ?? throw new InvalidOperationException(
                $"Result of step: {step.GetType().Name} returns 'null'");
        if (nextResult.MessagingContext == null)
        {
            throw new InvalidOperationException(
                $"Result of step {step.GetType().Name} doesn't have a '{nameof(MessagingContext)}'");
        }

        return nextResult;
    }
}
