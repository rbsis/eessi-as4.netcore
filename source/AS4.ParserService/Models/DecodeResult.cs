using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Serialization;

namespace AS4.ParserService.Models;

public class DecodeResult
{
    /// <summary>
    /// The <see cref="EbmsMessageType"/> of the Received Message.
    /// </summary>
    public EbmsMessageType ReceivedMessageType { get; set; }

    /// <summary>
    /// When the decoded message is an Error signalmessage, this property
    /// contains the Error-details that are present in the ErrorMessage
    /// </summary>
    public string? ErrorInformation { get; set; }

    /// <summary>
    /// The ebMS Id of the received AS4 Message
    /// </summary>
    public string? ReceivedEbmsMessageId { get; set; }

    /// <summary>
    /// The ebMS Id of the message that has been created
    /// </summary>
    /// <remarks>This is typically Error or Receipt since the Decode service creates
    /// the signal-message that must be used to respond to a received UserMessage.</remarks>
    public string? ResponseEbmsMessageId { get; set; }

    /// <summary>
    /// The Message that must be used as a Response
    /// </summary>
    public byte[] ResponseMessage { get; set; } = [];

    /// <summary>
    /// The <see cref="EbmsMessageType"/> of the ResponseMessage that has been created.
    /// </summary>
    public EbmsMessageType ResponseMessageType { get; set; }

    /// <summary>
    /// The FileName that can be used to save the created response message
    /// </summary>
    public string? ResponseMessageFileName { get; set; }

    /// <summary>
    /// The Attachments that were present as a payload in the received message.
    /// </summary>
    public PayloadInfo[] Payloads { get; set; } = [];

    public static DecodeResult CreateForBadRequest() => new()
    {
        ReceivedMessageType = EbmsMessageType.Unknown,
        ResponseMessageType = EbmsMessageType.Unknown,
    };

    public static DecodeResult CreateAccepted(EbmsMessageType messageType, string receivedEbmsMessageId, Error? error) => new()
    {
        ResponseMessageType = EbmsMessageType.Unknown,
        ErrorInformation = CreateErrorInformationString(error),
        ReceivedMessageType = messageType,
        ReceivedEbmsMessageId = receivedEbmsMessageId
    };

    public static DecodeResult CreateWithReceipt(IEnumerable<PayloadInfo> receivedPayloads, byte[] responseMessage, string receivedEbmsMessageId, string receiptEbmsMessageId) => new()
    {
        ReceivedMessageType = EbmsMessageType.UserMessage,
        ReceivedEbmsMessageId = receivedEbmsMessageId,
        Payloads = [.. receivedPayloads],
        ResponseMessage = responseMessage,
        ResponseEbmsMessageId = receiptEbmsMessageId,
        ResponseMessageType = EbmsMessageType.Receipt,
        ResponseMessageFileName = $"{receiptEbmsMessageId}.receipt"
    };

    public static DecodeResult CreateWithError(byte[] responseMessage, Error error, string receivedEbmsMessageId, string errorEbmsMessageId) => new()
    {
        ResponseMessage = responseMessage,
        ErrorInformation = CreateErrorInformationString(error),
        ResponseEbmsMessageId = errorEbmsMessageId,
        ReceivedMessageType = EbmsMessageType.UserMessage,
        ResponseMessageType = EbmsMessageType.Error,
        ReceivedEbmsMessageId = receivedEbmsMessageId,
        ResponseMessageFileName = $"{errorEbmsMessageId}.error",
    };

    private static string? CreateErrorInformationString(Error? error)
    {
        if (error?.ErrorLines == null || !error.ErrorLines.Any())
        {
            return null;
        }

        var soapEnv = AS4XmlSerializer.ToSoapEnvelopeDocument(AS4Message.Create(error));
        var signalNode = soapEnv.SelectSingleNode("//*[local-name()='SignalMessage']");
        return signalNode?.OuterXml;
    }
}
