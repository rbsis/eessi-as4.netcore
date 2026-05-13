using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Xml;
using Error = Eu.EDelivery.AS4.Model.Core.Error;
using SignalMessage = Eu.EDelivery.AS4.Xml.SignalMessage;

namespace Eu.EDelivery.AS4.Mappings.Core;

internal static class ErrorMap
{
    /// <summary>
    /// Maps from a XML representation with optional routing usermessage to a domain model representation of an AS4 error.
    /// </summary>
    /// <param name="xml">The XML representation to convert.</param>
    /// <param name="routing">The optional routing usermessage to include in the to be created error.</param>
    internal static Error Convert(SignalMessage xml, Maybe<RoutingInputUserMessage> routing)
    {
        ArgumentNullException.ThrowIfNull(xml.Error);

        var messageId = xml.MessageInfo.MessageId;
        var refToMessageId = xml.MessageInfo?.RefToMessageId;
        var timestamp = xml.MessageInfo?.Timestamp.ToDateTimeOffset() ?? DateTimeOffset.Now;

        IEnumerable<ErrorLine> lines = (xml.Error ?? [])
            .Where(l => l != null)
            .Select(l => new ErrorLine(
                GetErrorCodeFromXml(l.errorCode),
                l.severity.ToEnum(Severity.FAILURE),
                l.shortDescription.ToEnum(ErrorAlias.Other),
                l.origin.AsMaybe(),
                l.category.AsMaybe(),
                l.refToMessageInError.AsMaybe(),
                l.Description.AsMaybe().Select(d => new ErrorDescription(d.lang, d.Value)),
                l.ErrorDetail.AsMaybe()))
            .ToArray();

        return routing.Select(r => new Error(messageId, refToMessageId, timestamp, lines, r))
                      .GetOrElse(() => new Error(messageId, refToMessageId, timestamp, lines));
    }

    /// <summary>
    /// Maps from a domain model representation to a XML representation of an AS4 error.
    /// </summary>
    /// <param name="model">The domain model to convert.</param>
    internal static SignalMessage Convert(Error model)
    {
        static Xml.Error MapErrorLine(ErrorLine l) => new()
        {
            errorCode = l.ErrorCode.GetString(),
            severity = l.Severity.ToString(),
            origin = l.Origin.GetOrElse(() => null!),
            category = l.Category.GetOrElse(() => null!),
            refToMessageInError = l.RefToMessageInError.GetOrElse(() => null!),
            shortDescription = l.ShortDescription.ToString(),
            ErrorDetail = l.Detail.GetOrElse(() => null!),
            Description = l.Description
                               .Select(d => new Description { lang = d.Language, Value = d.Value })
                               .GetOrElse(() => null!)
        };

        return new()
        {
            MessageInfo = new()
            {
                MessageId = model.MessageId,
                RefToMessageId = model.RefToMessageId,
                Timestamp = model.Timestamp.LocalDateTime
            },
            Error = model.ErrorLines.Select(MapErrorLine).ToArray()
        };
    }

    private static ErrorCode GetErrorCodeFromXml(string errorCodeXml)
    {
        if (errorCodeXml == null)
        {
            return ErrorCode.Ebms0004;
        }

        return errorCodeXml
               .ToUpper()
               .Replace("EBMS:", string.Empty)
               .ToEnum(ErrorCode.Ebms0004);
    }
}
