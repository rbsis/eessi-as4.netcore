using System.Collections.ObjectModel;
using System.ComponentModel;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Mappings.Submit;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Strategies.Retriever;
using Eu.EDelivery.AS4.Validators;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ArgumentNullException = System.ArgumentNullException;

namespace Eu.EDelivery.AS4.Steps.Submit;

/// <summary>
/// Create an <see cref="AS4Message"/> from a <see cref="SubmitMessage"/>
/// </summary>
[Info("Create AS4 message for the submit message")]
[Description("Create an AS4 Message for the submit message")]
public class CreateAS4MessageStep : IStep
{
    private readonly ILogger<CreateAS4MessageStep> _logger;
    private readonly IPayloadRetrieverProvider _payloadRetrieverProvider;
    private readonly IValidator<SubmitMessage> _submitMessageValidator;
    private readonly ISubmitMessageMap _submitMessageMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAS4MessageStep" /> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="payloadRetrieverProvider"></param>
    /// <param name="submitMessageValidator"></param>
    /// <param name="submitMessageMap"></param>
    public CreateAS4MessageStep(
        ILogger<CreateAS4MessageStep> logger,
        IPayloadRetrieverProvider payloadRetrieverProvider,
        IValidator<SubmitMessage> submitMessageValidator,
        ISubmitMessageMap submitMessageMap)
    {
        _logger = logger;
        _payloadRetrieverProvider = payloadRetrieverProvider;
        _submitMessageValidator = submitMessageValidator;
        _submitMessageMap = submitMessageMap;
    }

    /// <summary>
    /// Start Mapping from a <see cref="SubmitMessage"/> 
    /// to an <see cref="AS4Message"/>
    /// </summary>
    /// <param name="messagingContext"></param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(messagingContext);

        var submitMessage = messagingContext.SubmitMessage
            ?? throw new InvalidOperationException(
                $"{nameof(CreateAS4MessageStep)} requires a SubmitMessage to create an AS4Message from but no AS4Message is present in the MessagingContext");
        if (messagingContext.SendingPMode == null)
        {
            _logger.LogDebug("No SendingPMode was found, only use information from SubmitMessage to create AS4 UserMessage");
        }

        ValidateSubmitMessage(submitMessage);

        _logger.LogTrace("Create UserMessage for SubmitMessage");
        var userMessage = _submitMessageMap.CreateUserMessage(submitMessage, messagingContext.SendingPMode ?? submitMessage.PMode);

        _logger.LogInformation("{LogTag} UserMessage with Id \"{MessageId}\" created from SubmitMessage",
            messagingContext.LogTag,
            userMessage.MessageId);
        var as4Message = AS4Message.Create(userMessage, messagingContext.SendingPMode);

        var attachments = await RetrieveAttachmentsForAS4MessageAsync(submitMessage.Payloads, cancellation);

        as4Message.AddAttachments(attachments);

        messagingContext.ModifyContext(as4Message);
        return await StepResult.SuccessAsync(messagingContext);
    }

    private void ValidateSubmitMessage(SubmitMessage submitMessage)
    {
        _submitMessageValidator.Validate(submitMessage)
            .Result(
                result => _logger.LogTrace("SubmitMessage \"{MessageId}\" is valid", submitMessage.MessageInfo.MessageId),
                result =>
                {
                    var description = result.AppendValidationErrorsToErrorMessage("SubmitMessage was invalid");

                    _logger.LogError(description);
                    throw new InvalidMessageException(description);

                });
    }

    private async Task<IEnumerable<Attachment>> RetrieveAttachmentsForAS4MessageAsync(IEnumerable<Payload> payloads, CancellationToken cancellation)
    {
        if (payloads == null || !payloads.Any())
        {
            _logger.LogTrace("SubmitMessage has no payloads to retrieve, so no will be added to the AS4Message");
            return [];
        }

        try
        {
            _logger.LogTrace("Start retrieving SubmitMessage payloads contents...");
            var attachments = await RetrieveAttachmentsAsync(payloads, cancellation);
            _logger.LogTrace("Successfully retrieved {Count} payloads", attachments.Count());

            return attachments;
        }
        catch (Exception exception)
        {
            const string Description = "Failed to retrieve SubmitMessage payloads";
            _logger.LogError(exception, Description);

            throw new InvalidOperationException(Description, exception);
        }
    }

    private async Task<IEnumerable<Attachment>> RetrieveAttachmentsAsync(IEnumerable<Payload> payloads, CancellationToken cancellation)
    {
        var attachments = new Collection<Attachment>();
        foreach (var payload in payloads)
        {
            if (payload == null)
            {
                throw new ArgumentException(@"SubmitMessage contains one or more payloads that was 'null'", nameof(payloads));
            }

            var missingValues = new[]
                {
                    payload.Id == null ? "Id" : null,
                    payload.Location == null ? "Location" : null,
                    payload.MimeType == null ? "MimeType" : null
                }.Where(s => s != null)
                 .Select(s => $"'{s}'");

            if (missingValues.Any())
            {
                throw new InvalidOperationException(
                    $"Submit payload is not complete to retrieve the contents, missing values: {string.Join(", ", missingValues)}");
            }

            var content = await RetrievePayloadContentsAsync(payload, cancellation);

            _logger.LogTrace("Add attachment {PayloadId} {MimeType} to AS4Message", payload.Id, payload.MimeType);
            attachments.Add(new Attachment(payload.Id!, content, payload.MimeType!));
        }

        return attachments;
    }

    private async Task<Stream> RetrievePayloadContentsAsync(Payload payload, CancellationToken cancellation)
    {
        var retriever = _payloadRetrieverProvider.Get(payload);

        return await retriever.RetrievePayloadAsync(payload.Location!, cancellation);
    }
}
