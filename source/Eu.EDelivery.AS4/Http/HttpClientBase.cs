using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Http;

public class HttpClientBase
{
    protected ILogger<HttpClientBase> Logger { get; }

    protected HttpClientBase(ILogger<HttpClientBase> logger)
    {
        Logger = logger;
    }

    protected async Task<HttpResponseMessage> PostRequestAsync(string url, HttpContent content, X509Certificate? clientCert, CancellationToken cancellation)
    {
        try
        {
            var handler = new HttpClientHandler();
            if (clientCert != null)
            {
                handler.ClientCertificates.Add(clientCert);
            }

            using var client = new HttpClient(handler);
            var response = await client.PostAsync(url, content, cancellation);
            if (response == null)
            {
                Logger.LogError("No HttpResponseMessage received for http notification.");
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            if (!IsResponseValid(response))
            {
                await LogErrorResponseAsync(response, cancellation);
            }

            return response;
        }
        catch (HttpRequestException exception)
        {
            Logger.LogError(exception, "Failed to Send PostRequest to Url: {Url}.", url);
            return new HttpResponseMessage(exception.StatusCode ?? HttpStatusCode.InternalServerError);
        }
    }

    private static bool IsResponseValid(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.OK;

    private async Task LogErrorResponseAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        var responseString = await response.Content.ReadAsStringAsync(cancellation);
        if (string.IsNullOrEmpty(responseString) || !Logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        Logger.LogError("Unexpected response received for http notification: {ResponseStatusCode}, {ResponseString}",
            response.StatusCode,
            responseString);
    }
}
