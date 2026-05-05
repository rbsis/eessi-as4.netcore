
// ReSharper disable once CheckNamespace
// Naming convention according to https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0
using Eu.EDelivery.AS4.ServiceHandler;
using Eu.EDelivery.AS4.ServiceHandler.Providers;

namespace Microsoft.Extensions.DependencyInjection;

public static class AS4ServiceCollectionExtensions
{
    public static IServiceCollection AddAS4ServiceHandler(this IServiceCollection serviceCollection) => serviceCollection
        .AddTransient<AgentProvider>()
        .AddTransient<Kernel>();

}
