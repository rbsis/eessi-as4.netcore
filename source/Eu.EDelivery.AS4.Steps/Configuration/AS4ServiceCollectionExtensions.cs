
// ReSharper disable once CheckNamespace
// Naming convention according to https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0
using System.Reflection;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Receive.Participant;
using Eu.EDelivery.AS4.Steps.Send.Response;

namespace Microsoft.Extensions.DependencyInjection;

public static class AS4ServiceCollectionExtensions
{
    public static IServiceCollection AddAS4Steps(this IServiceCollection serviceCollection) => serviceCollection
        .Scan(scan => scan
            .FromAssemblies(Assembly.GetExecutingAssembly())
            .AddClasses(filter => filter.AssignableTo<IStep>(), false)
            .AsSelf()
            .WithSingletonLifetime())
        .Scan(scan => scan
            .FromAssemblies(Assembly.GetExecutingAssembly())
            .AddClasses(filter => filter.AssignableTo<IConfigStep>(), false)
            .AsSelf()
            .WithTransientLifetime())
        .AddSingleton<IDefaultAgentStepRegistry, DefaultAgentStepRegistry>()
        .AddSingleton<IPModeRuleEngine, PModeRuleEngine>()
        .AddKeyedSingleton<IAS4ResponseHandler, TailResponseHandler>(typeof(TailResponseHandler))
        .AddKeyedSingleton<IAS4ResponseHandler, EmptyBodyResponseHandler>(typeof(EmptyBodyResponseHandler))
        .AddKeyedSingleton<IAS4ResponseHandler, PullRequestResponseHandler>(typeof(PullRequestResponseHandler));
}
