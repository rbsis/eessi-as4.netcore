using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;

namespace Eu.EDelivery.AS4.Mappings.Submit;

public interface ISubmitMessageMap
{
    UserMessage CreateUserMessage(SubmitMessage submit, SendingProcessingMode? sendingPMode);
}
