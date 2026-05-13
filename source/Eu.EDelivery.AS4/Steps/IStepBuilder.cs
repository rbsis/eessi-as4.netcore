using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.Steps;

public interface IStepBuilder
{
    IStep BuildAsSingleStep(Step[] stepConfiguration);
    IStep BuildAsSingleStep(ConditionalStepConfig conditionalStepConfig);
    IEnumerable<IStep> BuildSteps(Step[] stepConfiguration);
    IEnumerable<IStep> BuildSteps(ConditionalStepConfig conditionalStepConfig);
}
