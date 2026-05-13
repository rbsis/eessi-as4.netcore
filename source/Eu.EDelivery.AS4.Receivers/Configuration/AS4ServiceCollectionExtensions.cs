using System.Reflection;
using Eu.EDelivery.AS4.Agents;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Receivers;

// ReSharper disable once CheckNamespace
// Naming convention according to https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0
namespace Microsoft.Extensions.DependencyInjection;

public static class AS4ServiceCollectionExtensions
{
    public static IServiceCollection AddAS4Receivers(this IServiceCollection serviceCollection) => serviceCollection
        .AddOptions()
        .Configure<DatastoreReceiverSettings>(o =>
        {
            o.TableName = "RetryReliability";
            o.Filter = "Status = 'Pending'";
            o.UpdateField = "Status";
            o.UpdateValue = "Busy";
        })
        .Scan(scan => scan
            .FromAssemblies(Assembly.GetExecutingAssembly())
            .AddClasses(filter => filter.AssignableTo<IReceiver>(), false)
            .AsSelf()
            .WithTransientLifetime());

    public static IServiceCollection AddAS4Receiver<T>(this IServiceCollection serviceCollection, AgentType type) where T : class, IReceiver => serviceCollection
        .AddKeyedSingleton<IReceiver, T>(type);

    public static IServiceCollection AddAS4Receiver<T>(this IServiceCollection serviceCollection, AgentType type, Action<Receiver> configure) where T : class, IReceiver => serviceCollection
        .AddTransient<T>()
        .AddKeyedSingleton<IReceiver, T>(type, (sp, key) =>
        {
            var config = new Receiver();
            configure(config);
            config.Type = typeof(T).AssemblyQualifiedName;

            var receiverBuilder = sp.GetRequiredService<IReceiverBuilder>();
            return (T)receiverBuilder.BuildFromConfig(config);
        });
}
