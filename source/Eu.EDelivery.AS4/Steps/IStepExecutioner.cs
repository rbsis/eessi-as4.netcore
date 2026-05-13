using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Steps;

public interface IStepExecutioner
{
    Task<StepResult> ExecuteStepsAsync(MessagingContext currentContext, CancellationToken cancellation);
}
