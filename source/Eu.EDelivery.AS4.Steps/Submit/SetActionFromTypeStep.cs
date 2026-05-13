using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Submit;

namespace Eu.EDelivery.AS4.Steps.Submit;

public class SetActionFromTypeStep : IConfigStep
{
    private IDictionary<string, string> _properties = new Dictionary<string, string>();

    public void Configure(IDictionary<string, string> properties)
    {
        _properties = properties;
    }

    public async Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation)
    {
        var submitMessage = messagingContext.SubmitMessage
            ?? throw new InvalidOperationException(
                $"{nameof(SetActionFromTypeStep)} requires a SubmitMessage to set an Action no SubmitMessage is present in the MessagingContext");

        var action = GetAction(submitMessage);
        if (action != null)
        {
            submitMessage.Collaboration.Action = action;
        }

        return await StepResult.SuccessAsync(messagingContext);
    }

    private string? GetAction(SubmitMessage submitMessage)
    {
        var property = submitMessage.MessageProperties.FirstOrDefault(p => p.Name == "Type");

        return property != null ? _properties.ReadOptionalProperty(property.Value) : null;
    }

}
