using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Mappings.PMode;
public interface ISendingPModeMap
{
    UserMessage CreateUserMessage(SendingProcessingMode sendingPMode, params PartInfo[] parts);
    string ResolveAction(SendingProcessingMode? pmode);
    Model.Core.Service ResolveService(SendingProcessingMode? pmode);
}
