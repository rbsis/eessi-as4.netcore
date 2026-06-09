using Eu.EDelivery.AS4.Fe.Exceptions;
using Eu.EDelivery.AS4.Fe.Monitor;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Options;

namespace Eu.EDelivery.AS4.Fe.SubmitTool;

/// <summary>
///     Implementation of ISubmitMessageCreator
/// </summary>
/// <seealso cref="ISubmitMessageCreator" />
public class SubmitMessageCreator : ISubmitMessageCreator
{
    private readonly IClient _client;
    private readonly IEnumerable<IMessageHandler> _messageHandlers;
    private readonly IOptions<SubmitToolOptions> _options;
    private readonly IEnumerable<IPayloadHandler> _payloadHandlers;
    private readonly IPmodeService _pmodeService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SubmitMessageCreator" /> class.
    /// </summary>
    /// <param name="pmodeService">The pmode service.</param>
    /// <param name="payloadHandlers">The payload handlers.</param>
    /// <param name="messageHandlers">The message handlers.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="client">The SignalR client.</param>
    public SubmitMessageCreator(IPmodeService pmodeService, IEnumerable<IPayloadHandler> payloadHandlers, IEnumerable<IMessageHandler> messageHandlers, IOptions<SubmitToolOptions> options, IClient client)
    {
        _pmodeService = pmodeService;
        _payloadHandlers = payloadHandlers;
        _messageHandlers = messageHandlers;
        _options = options;
        _client = client;
    }

    /// <summary>
    ///     Submit one or more message(s)
    /// </summary>
    /// <param name="submitInfo">The submit information.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="BusinessException">
    ///     Missing to location
    ///     or
    ///     Missing payload location
    ///     or
    ///     Invalid number of submit messages value. Only a value between 1 &amp; 999 is allowed.
    ///     or
    ///     Could not find pmode
    /// </exception>
    public async Task CreateSubmitMessagesAsync(MessagePayload submitInfo, CancellationToken cancellationToken)
    {
        try
        {
            if (submitInfo.NumberOfSubmitMessages <= 0 || submitInfo.NumberOfSubmitMessages > 999)
            {
                throw new BusinessException("Invalid number of submit messages value. Only a value between 1 & 999 is allowed.");
            }

            await _client.SendInfoAsync($"Looking up PMode {submitInfo.SendingPmode}", cancellationToken);

            var sendingPmode = await _pmodeService.GetSendingByNameAsync(submitInfo.SendingPmode, cancellationToken)
                ?? throw new BusinessException("Could not find PMode");

            await _client.SendPmodeAsync(AS4XmlSerializer.ToString(sendingPmode.Pmode), cancellationToken);

            await CreateSubmitMessageObjectsAsync(submitInfo, sendingPmode.Pmode, _options.Value.PayloadHttpAddress, _options.Value.ToHttpAddress, cancellationToken);
        }
        catch (Exception ex)
        {
            await _client.SendErrorAsync(ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task CreateSubmitMessageObjectsAsync(MessagePayload submitInfo, SendingProcessingMode? sendingPmode, string payloadDestination, string messageDestination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sendingPmode, nameof(sendingPmode));

        var payloads = await CreatePayloadsAsync(submitInfo, payloadDestination, cancellationToken);

        for (var i = 0; i < submitInfo.NumberOfSubmitMessages; i++)
        {
            await _client.SendInfoAsync($"Submitting message {i + 1} of {submitInfo.NumberOfSubmitMessages} to {messageDestination}", cancellationToken);
            var submitMessage = BuildMessage(submitInfo, sendingPmode, payloads);
            await SubmitMessageAsync(submitMessage, messageDestination, cancellationToken);
        }
    }

    private async Task<List<FilePayload>> CreatePayloadsAsync(MessagePayload submitInfo, string payloadDestination, CancellationToken cancellationToken)
    {
        var payloads = new List<FilePayload>();

        foreach (var payloadInfo in submitInfo.Files)
        {
            var messagePayload = new FilePayload
            {
                MimeType = payloadInfo.ContentType,
                Location = await ProcessFileAsync(payloadInfo.Data, payloadInfo.FileName, payloadDestination, cancellationToken),
                FileName = payloadInfo.FileName
            };

            payloads.Add(messagePayload);
        }
        return payloads;
    }

    private static SubmitMessage BuildMessage(MessagePayload submitInfo, SendingProcessingMode sendingPmode, List<FilePayload> payloads)
    {
        var messageId = $"{Guid.NewGuid()}@{Environment.MachineName}";
        var submitMessage = new SubmitMessage
        {
            MessageInfo = { MessageId = messageId },
            Payloads = [.. payloads.Select(x => x.ToPayload(CreatePayloadId(submitInfo, x.FileName, messageId)))]
        };
        submitMessage.Collaboration.AgreementRef = new Agreement { PModeId = sendingPmode.Id };
        return submitMessage;
    }

    private static string CreatePayloadId(MessagePayload submitInfo, string fileName, string messageId)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        return submitInfo.NumberOfSubmitMessages > 1 ? $"{messageId}.{name}" : name;
    }

    private async Task<string> ProcessFileAsync(Stream stream, string fileName, string toLocation, CancellationToken cancellationToken)
    {
        await _client.SendInfoAsync($"Submitting payload \"{fileName}\" to {toLocation}", cancellationToken);
        var handler = _payloadHandlers.FirstOrDefault(x => x.CanHandle(toLocation))
            ?? throw new InvalidOperationException($"No payload handler found for {toLocation}");

        var result = await handler.HandleAsync(toLocation, fileName, stream, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to process payload \"{fileName}\" for {toLocation}");

        await _client.SendInfoAsync($"\"{fileName}\" has id {result}", cancellationToken);
        return result;
    }

    private async Task SubmitMessageAsync(SubmitMessage message, string toLocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message.MessageInfo.MessageId, nameof(message.MessageInfo.MessageId));

        await _client.SendAs4MessageAsync(AS4XmlSerializer.ToString(message), message.MessageInfo.MessageId, cancellationToken);
        var handler = _messageHandlers.First(x => x.CanHandle(toLocation))
            ?? throw new Exception($"No message handler found for {toLocation}");

        await handler.HandleAsync(message, toLocation, cancellationToken);
    }
}
