using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Streaming;
using Eu.EDelivery.AS4.Utilities;
using Microsoft.Extensions.Logging;
using Function =
    System.Func<Eu.EDelivery.AS4.Model.Internal.ReceivedMessage, System.Threading.CancellationToken,
        System.Threading.Tasks.Task<Eu.EDelivery.AS4.Model.Internal.MessagingContext>>;

namespace Eu.EDelivery.AS4.Receivers.Http;

/// <summary>
/// Receiver which listens on a given target URL
/// </summary>
[Info("HTTP receiver")]
public sealed class HttpReceiver : IReceiver, IDisposable
{
    private readonly HttpListener _listener;

    private readonly ILogger<HttpReceiver> _logger;
    private readonly IRouter _router;

    [Info("Url", required: true)]
    [Description("The URL to receive messages on. The url can also contain a port ex: http://localhost:5000/msh/")]
    private string Url { get; set; }

    [Info("Maximum concurrent requests to process", defaultValue: 10)]
    [Description("Indicates how wany requests should be processed per batch.")]
    private int ConcurrentRequests { get; set; } = 10;

    [Info("Use logging", defaultValue: false)]
    [Description("Log incoming requests to logs\\receivedmessages\\.")]
    private bool UseLogging { get; set; }

    public HttpReceiver(ILogger<HttpReceiver> logger, IRouter router)
    {
        _logger = logger;
        _router = router;
        _listener = new HttpListener();
        Url = string.Empty;
    }


    /// <summary>
    /// Configure the receiver with a given settings dictionary.
    /// </summary>
    /// <param name="settings"></param>
    public void Configure(IEnumerable<Setting> settings)
    {
        var properties = settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        const int DefaultConcurrentRequests = 10;
        var concurrentRequestValue = properties.ReadOptionalProperty(SettingKeys.ConcurrentRequests, DefaultConcurrentRequests.ToString());
        if (int.TryParse(concurrentRequestValue, out var maxConcurrentConnections))
        {
            ConcurrentRequests = maxConcurrentConnections;
        }
        else
        {
            _logger.LogWarning("Invalid \"{ConcurrentRequests}\" was given: {ConcurrentRequestValue}, will fall back to \"{DefaultConcurrentRequests}\"",
                SettingKeys.ConcurrentRequests,
                concurrentRequestValue,
                DefaultConcurrentRequests);
            ConcurrentRequests = DefaultConcurrentRequests;
        }

        var useLoggingValue = properties.ReadOptionalProperty(SettingKeys.UseLogging, defaultValue: false.ToString());
        _ = bool.TryParse(useLoggingValue, out var useLogging);
        UseLogging = useLogging;

        var hostname = properties.ReadMandatoryProperty(SettingKeys.Url);
        if (!hostname.EndsWith('/'))
        {
            Url = $"{hostname}/";
        }
        else
        {
            Url = hostname;
        }
    }

    /// <summary>
    /// Start receiving on a configured Target
    /// Received messages will be send to the given Callback
    /// </summary>
    /// <param name="messageCallback"></param>
    /// <param name="cancellationToken"></param>
    public void StartReceiving(Function messageCallback, CancellationToken cancellationToken)
    {
        try
        {
            _listener.Prefixes.Add(Url);
            StartListener(_listener);
            AcceptConnections(_listener, messageCallback, cancellationToken);
        }
        finally
        {
            _listener.Close();
        }
    }

    private void StartListener(HttpListener listener)
    {
        try
        {
            listener.Start();

            _logger.LogDebug("Start receiving on \"{Url}\" ...", Url);
            _logger.LogDebug("      with max concurrent connections = {ConcurrentRequests}", ConcurrentRequests);
            _logger.LogDebug("      with logging = {UseLogging}", UseLogging);
        }
        catch (HttpListenerException exception)
        {
            _logger.LogError(exception, "Http Listener Exception");
        }
    }

    private void AcceptConnections(
        HttpListener listener,
        Function messageCallback,
        CancellationToken cancellation)
    {
        GuardMaxConcurrentHttpConnections(
            listener,
            cancellation,
            processRequestAsync: async context =>
            {
                _logger.LogInformation("Received {HttpMethod} request at \"{RawUrl}\"",
                    context.Request.HttpMethod,
                    context.Request.RawUrl);
                await _router.RouteWithAsync(
                    httpContext: context,
                    prePostSelector: req => RunRequestThroughAgentAsync(req, messageCallback));

                context.Response.Close();
            });
    }

    private void GuardMaxConcurrentHttpConnections(
        HttpListener listener,
        CancellationToken cancellationToken,
        Func<HttpListenerContext, Task> processRequestAsync)
    {
        // The Semaphore makes sure the the maximum amount of concurrent connections is respected.
        using var semaphore = new Semaphore(ConcurrentRequests, ConcurrentRequests);
        while (listener.IsListening && !cancellationToken.IsCancellationRequested)
        {
            semaphore.WaitOne();

            try
            {
                if (!listener.IsListening)
                {
                    return;
                }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                listener.GetContextAsync()
                        .ContinueWith(async httpContextTask =>
                        {
                            // A request is being handled, so decrease the semaphore which will allow 
                            // that we're listening on another context.
                            semaphore.Release();

                            var context = await httpContextTask;

                            await processRequestAsync(context);

                            context.Response.Close();
                        });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed                 
            }
            catch (HttpListenerException ex)
            {
                _logger.LogTrace(ex, "Http Listener on {Url} stopped receiving requests.", Url);
            }
            catch (ObjectDisposedException)
            {
                // Not doing anything on purpose.
                // When a HttpListener is stopped, the context where being listened on is called one more time, 
                // but the context is disposed.  Therefore an exception is thrown.  Catch the exception to prevent
                // the process to end, but do nothing with the exception since this is by design.
            }
        }
    }

    private async Task<MessagingContext> RunRequestThroughAgentAsync(HttpListenerRequest request, Function messageCallback)
    {

        var message = await CreateReceivedMessageAsync(request);
        try
        {
            return await messageCallback(message, CancellationToken.None)
                        ;
        }
        finally
        {
            await message.UnderlyingStream.DisposeAsync();
        }
    }

    private async Task<ReceivedMessage> CreateReceivedMessageAsync(HttpListenerRequest request)
    {
        var message = await WrapRequestInSeekableMessageAsync(request, request.ContentLength64);

        if (UseLogging)
        {
            await LogReceivedMessageMessageAsync(message, request.Url);
        }

        return message;
    }

    private async Task<ReceivedMessage> WrapRequestInSeekableMessageAsync(HttpListenerRequest request, long contentLength)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.ContentType);

        _logger.LogTrace("Start copying to VirtualStream");
        var dest = new VirtualStream(
            request.ContentLength64 > VirtualStream.ThresholdMax
                ? VirtualStream.MemoryFlag.OnlyToDisk
                : VirtualStream.MemoryFlag.AutoOverFlowToDisk,
            forAsync: true);

        if (contentLength > 0)
        {
            dest.SetLength(contentLength);
        }

        await request.InputStream.CopyToAsync(dest);

        dest.Position = 0;

        return new ReceivedMessage(
            underlyingStream: dest,
            contentType: request.ContentType,
            origin: request.UserHostAddress,
            length: request.ContentLength64);
    }

    private async Task LogReceivedMessageMessageAsync(ReceivedMessage message, Uri? url)
    {
        const string LogDir = @".\logs\receivedmessages\";

        if (!Directory.Exists(LogDir))
        {
            Directory.CreateDirectory(LogDir);
        }

        var hostInformation = url != null ? $"{url.Host}_{url.Port}" : "localhost";

        try
        {
            var newReceivedMessageFile =
                FilenameUtils.EnsureValidFilename($"{hostInformation}.{Guid.NewGuid()}.{DateTime.Now:yyyyMMdd}");

            _logger.LogInformation("Logging to \"{NewReceivedMessageFile}\"", newReceivedMessageFile);

            using (var destinationStream =
                FileUtils.CreateAsync(
                    Path.Combine(LogDir, newReceivedMessageFile),
                    FileOptions.SequentialScan))
            {
                await message.UnderlyingStream.CopyToAsync(destinationStream);
            }

            message.UnderlyingStream.Position = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LogReceivedMessageMessage failed");
            throw;
        }
    }

    /// <summary>
    /// Stop the <see cref="IReceiver"/> instance from receiving.
    /// </summary>
    public void StopReceiving()
    {
        _logger.LogDebug("Stop listening on \"{Url}\"", Url);

        _listener?.Close();
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    [SuppressMessage(
        category: "Microsoft.Usage",
        checkId: "CA2213:DisposableFieldsShouldBeDisposed",
        MessageId = "_listener",
        Justification = "Warning but not justified")]
    public void Dispose()
    {
        try
        {
            ((IDisposable)_listener)?.Dispose();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Dispose failed");
        }
    }

    /// <summary>
    /// Data Class that contains the required keys to correctly configure the <see cref="HttpReceiver"/>.
    /// </summary>
    private static class SettingKeys
    {
        public const string Url = "Url";
        public const string ConcurrentRequests = "MaxConcurrentRequests";
        public const string UseLogging = "UseLogging";
    }
}
