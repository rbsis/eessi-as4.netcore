using System.ComponentModel;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Repositories;

namespace Eu.EDelivery.AS4.Steps.Send;

[Info("Log unexpected errors")]
[Description(
    "This step makes sure that unexpected errors are logged when something went wrong " +
    "during the send operation or during the processing of the synchronous response.")]
public class LogReceivedProcessingErrorStep : IStep
{
    private readonly IDatastoreRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogReceivedProcessingErrorStep" /> class.
    /// </summary>
    public LogReceivedProcessingErrorStep(IDatastoreRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Execute the step for a given <paramref name="messagingContext" />.
    /// </summary>
    /// <param name="messagingContext">Message used during the step execution.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        if (messagingContext.ErrorResult == null)
        {
            return await StepResult.SuccessAsync(messagingContext);
        }

        var exception = InException.ForEbmsMessageId(
            messagingContext.AS4Message?.FirstSignalMessage?.RefToMessageId,
            new Exception(messagingContext.ErrorResult.Description));

        _repository.InsertInException(exception);
        return await StepResult.SuccessAsync(messagingContext);
    }

}
