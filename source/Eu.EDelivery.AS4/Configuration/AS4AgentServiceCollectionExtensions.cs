using System.Xml;
using System.Xml.Serialization;
using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Exceptions.Handlers;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Services.Journal;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Transformers;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
// Naming convention according to https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0
namespace Microsoft.Extensions.DependencyInjection;

public static class AS4AgentServiceCollectionExtensions
{
    public static IServiceCollection AddAS4Agents(this IServiceCollection serviceCollection, string settingsFileName)
    {
        var settings = Deserialize<Settings>(settingsFileName);

        return serviceCollection
            .AddAS4Config(settingsFileName)
            .AddCustomAgents(AgentType.Notify, settings?.Agents?.NotifyAgents)
            .AddCustomAgents(AgentType.Deliver, settings?.Agents?.DeliverAgents)
            .AddCustomAgents(AgentType.PushSend, settings?.Agents?.SendAgents)
            .AddCustomAgents(AgentType.Submit, settings?.Agents?.SubmitAgents)
            .AddCustomAgents(AgentType.Receive, settings?.Agents?.ReceiveAgents)
            .AddCustomAgents(AgentType.PullReceive, settings?.Agents?.PullReceiveAgents)
            .AddCustomAgents(AgentType.PullSend, settings?.Agents?.PullSendAgents)
            .AddCustomAgents(AgentType.OutboundProcessing, settings?.Agents?.OutboundProcessingAgents)
            .AddCustomAgents(AgentType.Forward, settings?.Agents?.ForwardAgents)
            .AddAS4RetryAgent()
            .AddHostedService<CleanUpAgent>();
    }

    public static IServiceCollection AddAS4Agent(this IServiceCollection serviceCollection, AgentType type, string name, Action<AgentSettings> configure) => serviceCollection
        .AddHostedService(sp =>
        {
            var config = sp.GetAgentConfigFromSettings(type, name, configure);

            var agentLogTag = $"{config.Type} Agent {config.Name}";

            ArgumentNullException.ThrowIfNull(config.Settings?.StepConfiguration);

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
                receiver: sp.GetGetRecieverFromConfig(type, config.Settings.Receiver),
                transformer: sp.GetGetTransformerFromConfig(type, config.Settings.Transformer),
                exceptionHandler: exceptionHandler,
                steps: stepExecutioner,
                journalLogger: sp.GetRequiredKeyedService<IJournalLogger>(typeof(JournalDatastoreLogger)));
        });

    private static AgentConfig GetAgentConfigFromSettings(this IServiceProvider serviceProvider, AgentType type, string name, Action<AgentSettings> configure)
    {
        var defaultAgentReceiverRegistry = serviceProvider.GetService<IDefaultAgentReceiverRegistry>();
        var defaultAgentTransformerRegistry = serviceProvider.GetService<IDefaultAgentTransformerRegistry>();
        var defaultAgentStepRegistry = serviceProvider.GetService<IDefaultAgentStepRegistry>();

        var config = new AgentConfig(name)
        {
            Settings = new()
            {
                Name = name,
                Receiver = defaultAgentReceiverRegistry?.GetDefaultReceiver(type),
                Transformer = defaultAgentTransformerRegistry?.GetDefaultTransformer(type),
                StepConfiguration = defaultAgentStepRegistry?.GetDefaultStepConfiguration(type)
            }
        };
        configure(config.Settings);
        config.Type = type;

        return config;
    }

    private static IReceiver GetGetRecieverFromConfig(this IServiceProvider serviceProvider, AgentType type, Receiver? config)
    {
        if (config == null)
        {
            return serviceProvider.GetRequiredKeyedService<IReceiver>(type);
        }

        var receiverBuilder = serviceProvider.GetRequiredService<IReceiverBuilder>();
        return receiverBuilder.BuildFromConfig(config);
    }

    private static ITransformer GetGetTransformerFromConfig(this IServiceProvider serviceProvider, AgentType type, Transformer? config)
    {
        if (config == null)
        {
            return serviceProvider.GetRequiredKeyedService<ITransformer>(type);
        }

        var transformerBuilder = serviceProvider.GetRequiredService<ITransformerBuilder>();
        return transformerBuilder.BuildFromConfig(config);
    }

    private static T? Deserialize<T>(string path) where T : class
    {
        try
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            var serializer = new XmlSerializer(typeof(T));
            return serializer.Deserialize(fileStream) as T;
        }
        catch (Exception ex)
        {
            throw new XmlException("Invalid XML file: " + path, ex);
        }
    }

    private static IServiceCollection AddCustomAgents(this IServiceCollection serviceCollection, AgentType type, AgentSettings[]? agentsSettings)
    {
        if (agentsSettings == null)
        {
            return serviceCollection;
        }

        foreach (var agentSettings in agentsSettings)
        {
            serviceCollection.AddAS4Agent(type, agentSettings.Name, settings =>
            {
                settings.Receiver = agentSettings.Receiver;
                settings.Transformer = agentSettings.Transformer;
                settings.StepConfiguration = agentSettings.StepConfiguration;
            });
        }

        return serviceCollection;
    }

    public static IServiceCollection AddAS4RetryAgent(this IServiceCollection serviceCollection) => serviceCollection
        .AddHostedService(sp =>
        {
            return new RetryAgent(
                logger: sp.GetRequiredService<ILogger<RetryAgent>>(),
                receiver: sp.GetRequiredKeyedService<IReceiver>(AgentType.Retry),
                repository: sp.GetRequiredService<IDatastoreRepository>(),
                inMessageService: sp.GetRequiredService<IInMessageService>());
        });
}
