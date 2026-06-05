using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Eu.EDelivery.AS4.Fe.Monitor;

/// <summary>
/// SignalR messagehub used for communicating with the submit tool client(s)
/// </summary>
/// <seealso cref="Hub" />
[Authorize]
public class SubmitToolMessageHub : Hub
{
}
