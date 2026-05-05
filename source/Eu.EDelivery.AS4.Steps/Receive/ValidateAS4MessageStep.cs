using System.ComponentModel;
using System.Xml;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Steps.Receive;

[Info("Validate received AS4 Message")]
[Description("Verify if the received AS4 Message is valid for further processing")]
public class ValidateAS4MessageStep : IStep
{
    private static readonly XmlNamespaceManager _namespaces = new(new NameTable());

    private readonly ILogger<ValidateAS4MessageStep> _logger;

    public ValidateAS4MessageStep(ILogger<ValidateAS4MessageStep> logger)
    {
        _logger = logger;
    }

    static ValidateAS4MessageStep()
    {
        _namespaces.AddNamespace("soap12", Constants.Namespaces.Soap12);
    }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext"/>.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext.AS4Message == null)
        {
            throw new InvalidOperationException(
                $"{nameof(ValidateAS4MessageStep)} requires an AS4Message to validate but no AS4Message is present in the MessagingContext");
        }

        _logger.LogTrace("Validating the received AS4Message ...");
        if (SoapBodyIsNotEmpty(messagingContext.AS4Message))
        {
            messagingContext.ErrorResult = SoapBodyAttachmentsNotSupported();
            return ValidationFailure(messagingContext);
        }

        var notSupportedPartInfos =
           messagingContext.AS4Message.UserMessages.SelectMany(
               message => message.PayloadInfo.Where(payload => !payload.Href.StartsWith("cid:")));

        if (notSupportedPartInfos.Any())
        {
            messagingContext.ErrorResult = ExternalPayloadError(notSupportedPartInfos);
            return ValidationFailure(messagingContext);
        }

        if (messagingContext.AS4Message.IsUserMessage)
        {
            var message = messagingContext.AS4Message;

            var unreferencedPartInfos = message.UserMessages
                .SelectMany(u => u.PayloadInfo)
                .Where(p => message.Attachments.FirstOrDefault(a => a.Matches(p)) is null);

            if (unreferencedPartInfos.Any())
            {
                messagingContext.ErrorResult = AttachmentNotFoundInvalidHeaderError(unreferencedPartInfos);
                return ValidationFailure(messagingContext);
            }

            var duplicatePartInfos = message.UserMessages
                .SelectMany(u => u.PayloadInfo)
                .GroupBy(p => p.Href)
                .Where(g => g.Count() != 1);

            if (duplicatePartInfos.Any())
            {
                messagingContext.ErrorResult = DuplicateAttachmentInvalidHeaderError(duplicatePartInfos);
                return ValidationFailure(messagingContext);
            }
        }

        _logger.LogTrace("{LogTag} Received AS4Message is valid", messagingContext.LogTag);
        return await StepResult.SuccessAsync(messagingContext);
    }

    private static bool SoapBodyIsNotEmpty(AS4Message message)
    {
        var bodyNode = message.EnvelopeDocument?.SelectSingleNode("/soap12:Envelope/soap12:Body", _namespaces);

        return !string.IsNullOrWhiteSpace(bodyNode?.InnerText);
    }

    private static ErrorResult SoapBodyAttachmentsNotSupported()
    {
        return new ErrorResult(
            "AS4Message is not supported because there exists attachments in the SOAP body",
            ErrorAlias.FeatureNotSupported);
    }

    private static ErrorResult ExternalPayloadError(IEnumerable<PartInfo> notSupportedPartInfos)
    {
        return new ErrorResult(
            "Not all attachments are embedded in the MIME message and are referred "
            + $"in the PayloadInfo section using a PartInfo with a cid href reference: {string.Join(", ", notSupportedPartInfos.Select(p => p.Href))}",
            ErrorAlias.ExternalPayloadError);
    }

    private static ErrorResult AttachmentNotFoundInvalidHeaderError(IEnumerable<PartInfo> unreferencedPartInfos)
    {
        return new ErrorResult(
            $"No attachment can be found this/these PartInfo(s) in the UserMessage: {string.Join(", ", unreferencedPartInfos.Select(p => p.Href))}",
            ErrorAlias.InvalidHeader);
    }

    private static ErrorResult DuplicateAttachmentInvalidHeaderError(
        IEnumerable<IGrouping<string, PartInfo>> duplicatePartInfos)
    {
        return new ErrorResult(
            $"AS4Message is invalid because it contains duplicate PartInfo elements: {string.Join(", ", duplicatePartInfos.Select(g => g.Key))}",
            ErrorAlias.InvalidHeader);
    }

    private StepResult ValidationFailure(MessagingContext context)
    {
        _logger.LogError("{LogTag} AS4Message is not valid: {Description}", context.LogTag, context.ErrorResult?.Description);
        return StepResult.Failed(context);
    }
}
