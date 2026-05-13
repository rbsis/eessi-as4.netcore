using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Exceptions.Handlers;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Services.Journal;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Transformers;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
// Naming convention according to https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0
namespace Microsoft.Extensions.DependencyInjection;

public static class AS4AgentServiceCollectionExtensions
{
    public static IServiceCollection AddAS4Agent(this IServiceCollection serviceCollection, AgentType type, string name, Action<AgentSettings> configure) => serviceCollection
        .AddHostedService(sp =>
        {
            var config = new AgentConfig(name)
            {
                Settings = new() { Name = name }
            };
            configure(config.Settings);
            config.Type = type;

            var agentLogTag = $"{config.Type} Agent {config.Name}";

            ArgumentNullException.ThrowIfNull(config.Settings.StepConfiguration);

            if (config.Settings.StepConfiguration.NormalPipeline != null
                && config.Settings.StepConfiguration.NormalPipeline.Any(s => s?.Type == null))
            {
                throw new InvalidOperationException($@"{agentLogTag} has one ore more Steps in the NormalPipeline without a Type");
            }

            if (config.Settings.StepConfiguration.ErrorPipeline != null
                && config.Settings.StepConfiguration.ErrorPipeline.Any(s => s?.Type == null))
            {
                throw new ArgumentNullException($@"{agentLogTag} has one or more Steps in the ErrorPipeline without a Type");
            }

            var exceptionHandlerRegistry = sp.GetRequiredService<IExceptionHandlerRegistry>();
            var exceptionHandler = exceptionHandlerRegistry.GetHandler(config.Type);

            var stepBuilder = sp.GetRequiredService<IStepBuilder>();
            var stepExecutioner = new StepExecutioner(
                stepBuilder.BuildSteps(config.Settings.StepConfiguration.NormalPipeline ?? []),
                stepBuilder.BuildSteps(config.Settings.StepConfiguration.ErrorPipeline ?? []),
                exceptionHandler);

            return new Agent(
                logger: sp.GetRequiredService<ILogger<Agent>>(),
                config: config,
                receiver: sp.GetRequiredKeyedService<IReceiver>(type),
                transformer: sp.GetRequiredKeyedService<ITransformer>(type),
                exceptionHandler: exceptionHandler,
                steps: stepExecutioner,
                journalLogger: sp.GetRequiredKeyedService<IJournalLogger>(typeof(JournalDatastoreLogger)));
        });
}
