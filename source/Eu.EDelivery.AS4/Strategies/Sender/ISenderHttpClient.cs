using System.Net;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Notify;

namespace Eu.EDelivery.AS4.Strategies.Sender;

public interface ISenderHttpClient
{
    Task<HttpStatusCode> PostDeliverMessageEnvelopeAsync(string url, DeliverMessageEnvelope envelop, CancellationToken cancellation);
    Task<HttpStatusCode> PostNotifyMessageEnvelopeAsync(string url, NotifyMessageEnvelope envelop, CancellationToken cancellation);
}
