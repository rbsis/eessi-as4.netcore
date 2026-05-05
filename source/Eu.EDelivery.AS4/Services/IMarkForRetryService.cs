using Eu.EDelivery.AS4.Strategies.Sender;

namespace Eu.EDelivery.AS4.Services;

public interface IMarkForRetryService
{
    /// <summary>
    /// Updates the AS4Message's Status/Operation accordingly to the status of the 
    /// </summary>
    /// <param name="messageId"></param>
    /// <param name="status"></param>
    void UpdateAS4MessageForSendResult(long messageId, SendResult status);
    void UpdateDeliverMessageForDeliverResult(string messageId, SendResult status);
    void UpdateDeliverMessageForUploadResult(string messageId, SendResult status);
    void UpdateNotifyExceptionForIncomingMessage(long messageId, SendResult result);
    void UpdateNotifyExceptionForOutgoingMessage(long messageId, SendResult result);
    void UpdateNotifyMessageForIncomingMessage(long messageId, SendResult result);
    void UpdateNotifyMessageForOutgoingMessage(long messageId, SendResult result);
}
