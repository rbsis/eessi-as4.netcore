using System.Data;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps;

/// <summary>
/// Builder to make <see cref="IStep"/> implementation
/// from <see cref="Step"/> settings
/// </summary>
public class StepBuilder : IStepBuilder
{
    private readonly ILogger<StepBuilder> _logger;
    private readonly IServiceProvider _serviceProvider;

    public StepBuilder(ILogger<StepBuilder> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Build the <see cref="IStep"/> implementation
    /// </summary>
    /// <param name="stepConfiguration"></param>
    /// <returns></returns>
    public IStep BuildAsSingleStep(Step[] stepConfiguration)
    {
        var steps = stepConfiguration.Select(CreateInstance).ToArray();
        return new CompositeStep(steps);

    }
    /// <summary>
    /// Build the <see cref="IStep"/> implementation
    /// </summary>
    /// <param name="conditionalStepConfig">The conditional step configuration.</param>
    /// <returns></returns>
    public IStep BuildAsSingleStep(ConditionalStepConfig conditionalStepConfig) => new ConditionalStep(
        conditionalStepConfig.Condition,
        conditionalStepConfig.ThenSteps,
        conditionalStepConfig.ElseSteps,
        this);

    /// <summary>
    /// Builds the steps.
    /// </summary>
    /// <param name="stepConfiguration"></param>
    /// <returns></returns>
    public IEnumerable<IStep> BuildSteps(Step[] stepConfiguration)
    {
        return stepConfiguration.Select(CreateInstance);
    }

    /// <summary>
    /// Builds the steps.
    /// </summary>
    /// <param name="conditionalStepConfig">The conditional step configuration.</param>
    /// <returns></returns>
    public IEnumerable<IStep> BuildSteps(ConditionalStepConfig conditionalStepConfig) => [new ConditionalStep(
        conditionalStepConfig.Condition,
        conditionalStepConfig.ThenSteps,
        conditionalStepConfig.ElseSteps,
        this)];

    private IStep CreateInstance(Step settingStep)
    {
        return settingStep.Setting != null
            ? CreateConfigurableStep(settingStep.Type, settingStep.Setting)
            : CreateInstance<IStep>(settingStep.Type);
    }

    private T CreateInstance<T>(string typeString) where T : class
    {
        var type = Type.GetType(typeString, throwOnError: false);
        if (type == null)
        {
            _logger.LogError("Cannot resolve type string: {TypeString} to a {Name} instance because the type is not found in this AppDomain",
                typeString,
                typeof(T).Name);

            throw new InvalidOperationException($"Cannot resolve a valid {nameof(IStep)} implementation for the {typeString} fully-qualified assembly name");
        }

        return _serviceProvider.GetService(type) as T ??
            throw new InvalidOperationException($"Cannot resolve a valid {nameof(IStep)} implementation for the {typeString} fully-qualified assembly name");
    }

    private IConfigStep CreateConfigurableStep(string typeString, Setting[] settings)
    {
        var step = CreateInstance<IConfigStep>(typeString);

        var dictionary = settings.ToDictionary(s => s.Key, s => s.Value);

        step.Configure(dictionary);

        return step;
    }
}
