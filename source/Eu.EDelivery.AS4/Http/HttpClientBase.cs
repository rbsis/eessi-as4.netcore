using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Http;

public partial class HttpClientBase
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
            var handler = new HttpClientHandler
            {
                // Enforce TLS 1.2 or higher
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,

                // Enable certificate revocation checking
                CheckCertificateRevocationList = true,

                // Explicit server certificate validation
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // Perform custom validation if needed
                    // For now, use default validation but ensure revocation checking
                    if (errors != System.Net.Security.SslPolicyErrors.None)
                    {
                        Logger.LogWarning("SSL certificate validation failed for {Url}: {Errors}", url, errors);
                        return false;
                    }
                    return true;
                }
            };

            if (clientCert != null)
            {
                // Use X509Certificate2 for better security
                if (clientCert is X509Certificate2 cert2)
                {
                    handler.ClientCertificates.Add(cert2);
                }
                else
                {
                    // Convert to X509Certificate2 if possible
                    handler.ClientCertificates.Add(new X509Certificate2(clientCert));
                }
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

        // Sanitize sensitive data from response before logging
        var sanitizedResponse = SanitizeResponseForLogging(responseString);

        Logger.LogError("Unexpected response received for http notification: {ResponseStatusCode}, Response: {SanitizedResponse}",
            response.StatusCode,
            sanitizedResponse);
    }

    private static string SanitizeResponseForLogging(string responseContent)
    {
        if (string.IsNullOrEmpty(responseContent))
            return responseContent;

        // Limit response length to prevent log flooding
        const int MaxLength = 500;
        var truncated = responseContent.Length > MaxLength
            ? responseContent[..MaxLength] + "...[truncated]"
            : responseContent;

        // Remove or mask potential sensitive data patterns
        // Add more patterns as needed based on your data
        truncated = PasswordRegex().Replace(truncated, "<password>***</password>");
        truncated = TokenRegex().Replace(truncated, "<token>***</token>");
        truncated = CertificateRegex().Replace(truncated, "<certificate>***</certificate>");

        return truncated;
    }

    [GeneratedRegex(@"(?i)<certificate[^>]*>.*?</certificate>", RegexOptions.None, "nl-NL")]
    private static partial Regex CertificateRegex();

    [GeneratedRegex(@"(?i)<password[^>]*>.*?</password>", RegexOptions.None, "nl-NL")]
    private static partial Regex PasswordRegex();

    [GeneratedRegex(@"(?i)<token[^>]*>.*?</token>", RegexOptions.None, "nl-NL")]
    private static partial Regex TokenRegex();
}
