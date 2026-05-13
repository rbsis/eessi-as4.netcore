using System.Configuration;
using Eu.EDelivery.AS4.Agents;
using Microsoft.Extensions.Logging;

namespace Eu.EDelivery.AS4.Exceptions.Handlers;

/// <summary>
/// Registry to defining <see cref="IAgentExceptionHandler"/> implementations based on a given <see cref="AgentType"/>.
/// </summary>
internal class ExceptionHandlerRegistry : IExceptionHandlerRegistry
{
    private readonly IAgentExceptionHandler _outboundHandler;
    private readonly IAgentExceptionHandler _inboudHandler;
    private readonly IAgentExceptionHandler _notifyHandler;
    private readonly IAgentExceptionHandler _pullSendHandler;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S6672:Generic logger injection should match enclosing type", Justification = "<Pending>")]
    public ExceptionHandlerRegistry(
        ILogger<SafeExceptionHandler> logger,
        OutboundExceptionHandler outboundHandler,
        InboundExceptionHandler inboudHandler,
        NotifyExceptionHandler notifyHandler,
        PullSendAgentExceptionHandler pullSendHandler)
    {
        _outboundHandler = new SafeExceptionHandler(logger, outboundHandler);
        _inboudHandler = new SafeExceptionHandler(logger, inboudHandler);
        _notifyHandler = new SafeExceptionHandler(logger, notifyHandler);
        _pullSendHandler = new SafeExceptionHandler(logger, pullSendHandler);
    }

    /// <summary>
    /// Gets the handler.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns></returns>
    public IAgentExceptionHandler GetHandler(AgentType type) => type switch
    {
        AgentType.Submit => _outboundHandler,
        AgentType.Receive => _inboudHandler,
        AgentType.PushSend => _outboundHandler,
        AgentType.Deliver => _inboudHandler,
        AgentType.Notify => _notifyHandler,
        AgentType.PullReceive => _inboudHandler,
        AgentType.PullSend => _pullSendHandler,
        AgentType.OutboundProcessing => _outboundHandler,
        AgentType.Forward => _inboudHandler,
        _ => throw new ConfigurationErrorsException($"There is no Exception Handler defined for Agents of type '{type}'")
    };
}
