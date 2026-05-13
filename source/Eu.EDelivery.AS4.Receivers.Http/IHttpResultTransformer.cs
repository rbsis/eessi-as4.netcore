using System.Net;
using Eu.EDelivery.AS4.Model.Core;

namespace Eu.EDelivery.AS4.Receivers.Http;

internal interface IHttpResultTransformer
{
    HttpResult FromAS4Message(HttpStatusCode status, AS4Message message);
}
