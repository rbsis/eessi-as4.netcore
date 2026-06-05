using System.Reflection;
using Eu.EDelivery.AS4.Fe.Mappers;

// Naming convention according to https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0
namespace Microsoft.Extensions.DependencyInjection;

public static class MapperServiceCollectionExtensions
{
    public static IServiceCollection AddMappers(this IServiceCollection services, Assembly? assembly = null) => services
        .Scan(scan => scan
            .FromAssemblies(assembly ?? Assembly.GetCallingAssembly())
            .AddClasses(filter => filter.AssignableTo(typeof(IMapper<,>)), false)
            .AsImplementedInterfaces()
            .WithSingletonLifetime());
}
