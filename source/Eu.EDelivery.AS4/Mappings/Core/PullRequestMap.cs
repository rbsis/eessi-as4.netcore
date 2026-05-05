namespace Eu.EDelivery.AS4.Mappings.Core;

internal static class PullRequestMap
{
    /// <summary>
    /// Maps from a XML representation to a domain model representation of an AS4 pull request.
    /// </summary>
    /// <param name="xml">The XML representation to convert.</param>
    internal static Model.Core.PullRequest Convert(Xml.SignalMessage xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentException.ThrowIfNullOrEmpty(xml.MessageInfo.MessageId);

        return new Model.Core.PullRequest(xml.MessageInfo.MessageId, xml.PullRequest?.mpc);
    }

    /// <summary>
    /// Maps from a domain model representation to a XML representation of an AS4 pull request.
    /// </summary>
    /// <param name="model">The domain model to convert.</param>
    internal static Xml.SignalMessage Convert(Model.Core.PullRequest model) => new()
    {
        MessageInfo = new()
        {
            Timestamp = DateTime.Now.ToUniversalTime(),
            MessageId = model.MessageId,
        },
        PullRequest = new()
        {
            mpc = model.Mpc
        }
    };
}
