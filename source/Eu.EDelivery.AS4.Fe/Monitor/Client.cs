using Microsoft.AspNetCore.SignalR;

namespace Eu.EDelivery.AS4.Fe.Monitor;

/// <summary>
/// Implementation of the IClient to send messages using SignalR
/// </summary>
/// <seealso cref="IClient" />
public class Client : IClient
{
    private readonly IHubContext<SubmitToolMessageHub> _hub;

    public Client(IHubContext<SubmitToolMessageHub> hub)
    {
        _hub = hub;
    }

    /// <summary>
    /// Send info log
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SendInfoAsync(string message, CancellationToken cancellationToken)
    {
        await _hub.Clients.All.SendAsync("onMessage", new ClientMessage
        {
            Message = message
        }, cancellationToken);
    }

    /// <summary>
    /// Send log containing PMode
    /// </summary>
    /// <param name="pmode">The pmode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SendPmodeAsync(string pmode, CancellationToken cancellationToken)
    {
        await _hub.Clients.All.SendAsync("onMessage", new ClientMessage
        {
            Message = pmode,
            Type = LogType.Pmode
        }, cancellationToken);
    }

    /// <summary>
    /// Sendg log containing an AS4 message
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="id">The id of the AS4 message</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SendAs4MessageAsync(string message, string id, CancellationToken cancellationToken)
    {
        await _hub.Clients.All.SendAsync("onMessage", new ClientMessage
        {
            Message = message,
            Data = id,
            Type = LogType.Message
        }, cancellationToken);
    }

    /// <summary>
    /// Sends log containing error
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SendErrorAsync(string message, CancellationToken cancellationToken)
    {
        await _hub.Clients.All.SendAsync("onMessage", new ClientMessage
        {
            Message = message,
            Type = LogType.Error
        }, cancellationToken);
    }
}
