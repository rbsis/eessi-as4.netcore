using System.ComponentModel;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;
using Eu.EDelivery.AS4.Http;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Repositories;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Services;
using Eu.EDelivery.AS4.Steps.Send.Response;
using Eu.EDelivery.AS4.Strategies.Sender;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Send;

/// <summary>
/// Send <see cref="AS4Message" /> to the corresponding Receiving MSH
/// </summary>
[Info("Send AS4 Message to the configured receiver")]
[Description("This step makes sure that an AS4 Message that has been processed, is sent to its destination")]
public class SendAS4MessageStep : IStep
{
    private readonly ILogger<SendAS4MessageStep> _logger;
    private readonly IReliableHttpClient _httpClient;
    private readonly ICertificateRepository _certificateRepository;
    private readonly IMarkForRetryService _markForRetryService;
    private readonly IPiggyBackingService _piggyBackingService;
    private readonly IAS4ResponseHandler _pullRequestResponseHandler;
    private readonly ISerializerProvider _serializerProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendAS4MessageStep" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="client">Instance to handle the HTTP response.</param>
    /// <param name="certificateRepository"></param>
    /// <param name="markForRetryService"></param>
    /// <param name="piggyBackingService"></param>
    /// <param name="pullRequestResponseHandler"></param>
    /// <param name="serializerProvider"></param>
    public SendAS4MessageStep(
        ILogger<SendAS4MessageStep> logger,
        IReliableHttpClient client,
        ICertificateRepository certificateRepository,
        IMarkForRetryService markForRetryService,
        IPiggyBackingService piggyBackingService,
        [FromKeyedServices(typeof(PullRequestResponseHandler))] IAS4ResponseHandler pullRequestResponseHandler,
        ISerializerProvider serializerProvider)
    {
        _logger = logger;
        _httpClient = client;
        _certificateRepository = certificateRepository;
        _markForRetryService = markForRetryService;
        _piggyBackingService = piggyBackingService;
        _pullRequestResponseHandler = pullRequestResponseHandler;
        _serializerProvider = serializerProvider;
    }

    /// <summary>
    /// Send the <see cref="AS4Message" />
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext.ReceivedMessage == null && messagingContext.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(SendAS4MessageStep)} requires a MessagingContext with a ReceivedStream or an AS4Message to correctly send the message");
        }

        if (messagingContext.ReceivedMessage == null && messagingContext.AS4Message != null && messagingContext.AS4Message.IsPullRequest)
        {
            throw new InvalidOperationException(
                $"{nameof(SendAS4MessageStep)} expects a PullRequest AS4Message when the MessagingContext does not contain a ReceivedStream");
        }

        var pushConfig = GetPushConfiguration(messagingContext.SendingPMode, messagingContext.ReceivingPMode);
        if (pushConfig?.Protocol?.Url == null)
        {
            throw new ConfigurationErrorsException(
                "Message cannot be send because neither the Sending or Receiving PMode has a Protocol.Url child in a <PushConfiguration/> or <ResponseConfiguration/> element");
        }

        var as4Message = await DeserializeUnderlyingStreamIfPresentAsync(
            messagingContext.ReceivedMessage,
            otherwise: messagingContext.AS4Message,
            cancellation);

        try
        {
            var contentType = messagingContext.ReceivedMessage?.ContentType ?? messagingContext.AS4Message!.ContentType;
            var request = CreateWebRequest(pushConfig, contentType.Replace("charset=\"utf-8\"", ""));

            var response = await _httpClient.PostRequestAsync(request, messagingContext, cancellation);
            messagingContext.ModifyContext(as4Message);

            var result = SendResultUtils.DetermineSendResultFromHttpResonse(response.StatusCode);
            UpdateRetryStatusForMessage(messagingContext, result);

            return await _pullRequestResponseHandler.HandleResponseAsync(response, cancellation);
        }
        catch
        {
            UpdateRetryStatusForMessage(messagingContext, SendResult.RetryableFail);
            throw;
        }
    }

    private PushConfiguration? GetPushConfiguration(
        SendingProcessingMode? sendingPMode,
        ReceivingProcessingMode? receivingPMode)
    {
        if (sendingPMode != null)
        {
            _logger.LogTrace("Use SendingPMode {PModeId} PushConfiguration", sendingPMode.Id);
            return sendingPMode.PushConfiguration;
        }

        _logger.LogTrace("Use ReceivingPMode {PModeId} ReplyHandling.ResponseConfiguration", receivingPMode?.Id);
        return receivingPMode?.ReplyHandling?.ResponseConfiguration;
    }

    private async Task<AS4Message> DeserializeUnderlyingStreamIfPresentAsync(
        ReceivedMessage? rm,
        AS4Message? otherwise,
        CancellationToken cancellation)
    {
        if (rm == null)
        {
            return otherwise!;
        }

        rm.UnderlyingStream.Position = 0;

        var as4Message = await _serializerProvider
            .Get(rm.ContentType)
            .DeserializeAsync(rm.UnderlyingStream, rm.ContentType, cancellation);

        // TODO: the serializer already does this?
        rm.UnderlyingStream.Position = 0;

        return as4Message;
    }

    private IHttpRequest CreateWebRequest(PushConfiguration pushConfig, string contentType)
    {
        ArgumentException.ThrowIfNullOrEmpty(pushConfig.Protocol.Url);

        var url = pushConfig.Protocol.Url;
        _logger.LogTrace("Creating WebRequest to {Url}", url);

        var request = _httpClient.CreateRequest(url, contentType);
        var clientCert = RetrieveClientCertificate(pushConfig.TlsConfiguration);
        if (clientCert != null)
        {
            request.AddClientCertificates(clientCert);
        }

        return request;
    }

    private X509Certificate2? RetrieveClientCertificate(TlsConfiguration tlsConfig)
    {
        if (tlsConfig == null || !tlsConfig.IsEnabled || tlsConfig.ClientCertificateInformation == null)
        {
            return null;
        }

        _logger.LogTrace("Adding Client TLS Certificate to HTTP Request");
        return RetrieveTlsCertificate(tlsConfig) ?? throw new NotSupportedException(
                "The TLS certificate information specified in the PMode could not be used to retrieve the certificate");
    }

    private X509Certificate2? RetrieveTlsCertificate(TlsConfiguration configuration)
    {
        if (configuration.ClientCertificateInformation is ClientCertificateReference clientCertRef
            && clientCertRef.ClientCertificateFindValue is not null)
        {
            return _certificateRepository.GetCertificate(
                clientCertRef.ClientCertificateFindType,
                clientCertRef.ClientCertificateFindValue);
        }

        if (configuration.ClientCertificateInformation is PrivateKeyCertificate embeddedCertInfo
            && embeddedCertInfo.Certificate is not null)
        {
            return new X509Certificate2(
                rawData: Convert.FromBase64String(embeddedCertInfo.Certificate),
                password: embeddedCertInfo.Password,
                keyStorageFlags: X509KeyStorageFlags.Exportable);
        }

        return null;
    }

    private void UpdateRetryStatusForMessage(MessagingContext ctx, SendResult result)
    {
        if (ctx.MessageEntityId.HasValue)
        {
            _markForRetryService.UpdateAS4MessageForSendResult(
                messageId: ctx.MessageEntityId.Value,
                status: result);
        }

        if (ctx.AS4Message?.IsPullRequest == true)
        {
            _piggyBackingService.ResetSignalMessagesToBePiggyBacked(ctx.AS4Message.SignalMessages, result);
        }
    }
}
