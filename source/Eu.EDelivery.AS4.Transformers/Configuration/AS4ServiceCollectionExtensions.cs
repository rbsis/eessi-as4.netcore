using System.Reflection;
using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Transformers;

// ReSharper disable once CheckNamespace
// Naming convention according to https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0
namespace Microsoft.Extensions.DependencyInjection;

public static class AS4ServiceCollectionExtensions
{
    public static IServiceCollection AddAS4Transformers(this IServiceCollection serviceCollection) => serviceCollection
        .Scan(scan => scan
            .FromAssemblies(Assembly.GetExecutingAssembly())
            .AddClasses(filter => filter.AssignableTo<ITransformer>(), false)
            .AsSelf()
            .WithTransientLifetime())
        .AddAS4DefaultTransformers();

    private static IServiceCollection AddAS4DefaultTransformers(this IServiceCollection serviceCollection) => serviceCollection
        .AddAS4Transformer<DeliverMessageTransformer>(AgentType.Deliver)
        .AddAS4Transformer<SubmitMessageXmlTransformer>(AgentType.Submit)
        .AddAS4Transformer<AS4MessageTransformer>(AgentType.OutboundProcessing)
        .AddAS4Transformer<OutMessageTransformer>(AgentType.PushSend)
        .AddAS4Transformer<AS4MessageTransformer>(AgentType.PullSend)
        .AddAS4Transformer<ReceiveMessageTransformer>(AgentType.Receive)
        .AddAS4Transformer<NotifyMessageTransformer>(AgentType.Notify)
        .AddAS4Transformer<ForwardMessageTransformer>(AgentType.Forward)
        .AddAS4Transformer<PModeToPullRequestTransformer>(AgentType.PullReceive);

    public static IServiceCollection AddAS4Transformer<T>(this IServiceCollection serviceCollection, AgentType type) where T : class, ITransformer => serviceCollection
        .AddKeyedSingleton<ITransformer, T>(type);

    public static IServiceCollection AddAS4Transformer<T>(this IServiceCollection serviceCollection, AgentType type, Action<Transformer> configure) where T : class, ITransformer => serviceCollection
        .AddTransient<T>()
        .AddKeyedSingleton<ITransformer, T>(type, (sp, key) =>
        {
            var config = new Transformer();
            configure(config);
            config.Type = typeof(T).AssemblyQualifiedName;

            var receiverBuilder = sp.GetRequiredService<ITransformerBuilder>();
            return (T)receiverBuilder.BuildFromConfig(config);
        });
}
