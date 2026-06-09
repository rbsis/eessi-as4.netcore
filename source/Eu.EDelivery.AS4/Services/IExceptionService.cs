using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;

namespace Eu.EDelivery.AS4.Services;

public interface IExceptionService
{
    Task<InException> InsertIncomingAS4MessageExceptionAsync(Exception exception, string? ebmsMessageId, ReceivingProcessingMode? pmode, CancellationToken cancellation);
    Task InsertIncomingExceptionAsync(Exception exception, Stream messageStream, CancellationToken cancellation);
    Task<InException> InsertIncomingSubmitExceptionAsync(Exception exception, SubmitMessage submit, ReceivingProcessingMode? pmode, CancellationToken cancellation);
    Task<OutException> InsertOutgoingAS4MessageExceptionAsync(Exception exception, string? ebmsMessageId, long? entityId, SendingProcessingMode? pmode, CancellationToken cancellation);
    Task InsertOutgoingExceptionAsync(Exception exception, Stream messageStream, CancellationToken cancellation);
    Task<OutException> InsertOutgoingSubmitExceptionAsync(Exception exception, SubmitMessage submit, SendingProcessingMode? pmode, CancellationToken cancellation);
    void InsertRelatedRetryReliability(InException referenced, Model.PMode.RetryReliability? reliability);
    void InsertRelatedRetryReliability(OutException referenced, Model.PMode.RetryReliability? reliability);
}
