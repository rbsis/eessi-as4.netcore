using Eu.EDelivery.AS4.Receivers.Http;
using Eu.EDelivery.AS4.Receivers.Http.Get;
using Eu.EDelivery.AS4.Receivers.Http.Post;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
// Naming convention according to https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-5.0
namespace Microsoft.Extensions.DependencyInjection;

public static class AS4ServiceCollectionExtensions
{
    public static IServiceCollection AddAS4ReceiversHttp(this IServiceCollection serviceCollection) => serviceCollection
        .AddSingleton<HttpReceiver>()
        .AddSingleton<IHttpGetHandler, GetHtmlHandler>()
        .AddSingleton<IHttpGetHandler, GetImageHandler>()
        .AddSingleton<IHttpPostHandler, ExceptionPostHandler>()
        .AddSingleton<IHttpPostHandler, SubmitPostHandler>()
        .AddSingleton<IHttpPostHandler, AsyncSignalResponseHandler>()
        .AddSingleton<IHttpPostHandler, ForwardMessageResponseHandler>()
        .AddSingleton<IHttpPostHandler, PullRequestResponseHandler>()
        .AddSingleton<IHttpPostHandler, SyncSignalResponseHandler>()
        .AddSingleton<IHttpPostHandler, AcceptedResponseHandler>()
        .AddSingleton<IHttpResultTransformer, HttpResultTransformer>()
        .AddSingleton<IRouter, Router>(sp =>
        {
            var router = new Router(sp.GetRequiredService<ILogger<Router>>());
            foreach (var handler in sp.GetServices<IHttpGetHandler>())
            {
                router.Via(handler);
            }
            foreach (var handler in sp.GetServices<IHttpPostHandler>())
            {
                router.Via(handler);
            }
            return router;
        });
}
