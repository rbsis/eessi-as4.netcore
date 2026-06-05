using System.Net;
using Eu.EDelivery.AS4.Fe.Monitor.Model;
using Eu.EDelivery.AS4.Fe.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eu.EDelivery.AS4.Fe.Controllers;

/// <summary>
/// Monitor controller
/// </summary>
/// <seealso cref="Controller" />
[Route("api/[controller]")]
public class MonitorController : Controller
{
    private readonly IMonitorService _monitorService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MonitorController"/> class.
    /// </summary>
    /// <param name="monitorService">The monitor service.</param>
    public MonitorController(IMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    /// <summary>
    /// Gets the in exceptions.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>ExceptionMessage</returns>
    [HttpGet]
    [Route("exceptions")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Exceptions retrieved successfully", typeof(MessageResult<ExceptionMessage>))]
    [SwaggerResponse((int)HttpStatusCode.ExpectationFailed, "No messages are found", typeof(ErrorModel))]
    public async Task<IActionResult> GetInExceptions(ExceptionFilter filter, CancellationToken cancellationToken)
    {
        return new OkObjectResult(await _monitorService.GetExceptionsAsync(filter, cancellationToken));
    }

    /// <summary>
    /// Gets the messages.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("messages")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Messages retrieved successfully", typeof(MessageResult<Message>))]
    [SwaggerResponse((int)HttpStatusCode.ExpectationFailed, "No messages are found", typeof(ErrorModel))]
    public async Task<IActionResult> GetMessages(MessageFilter filter, CancellationToken cancellationToken)
    {
        return new OkObjectResult(await _monitorService.GetMessagesAsync(filter, cancellationToken));
    }

    /// <summary>
    /// Gets the related messages.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("relatedmessages")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Related messages retrieved successfully", typeof(MessageResult<Message>))]
    public async Task<IActionResult> GetRelatedMessages(Direction direction, string messageId, CancellationToken cancellationToken)
    {
        return new OkObjectResult(await _monitorService.GetRelatedMessagesAsync(direction, messageId, cancellationToken));
    }

    /// <summary>
    /// Gets the message body.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="id">The message identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("messagebody")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Message body retrieved successfully", typeof(MessageResult<Message>))]
    public async Task<FileStreamResult> GetMessageBody(Direction direction, long id, CancellationToken cancellationToken)
    {
        return File(await _monitorService.DownloadMessageBodyAsync(direction, id, cancellationToken), "application/xml");
    }

    /// <summary>
    /// Gets the exception body.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="id">The message identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("exceptionbody")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Exception body retrieved successfully", typeof(MessageResult<ExceptionMessage>))]
    public async Task<FileResult> GetExceptionBody(Direction direction, long id, CancellationToken cancellationToken)
    {
        return File(await _monitorService.DownloadExceptionMessageBodyAsync(direction, id, cancellationToken), "application/txt");
    }

    [HttpGet]
    [Route("detail/{direction}/{id}")]
    public async Task<IActionResult> GetExceptionDetail(Direction direction, long id, CancellationToken cancellationToken)
    {
        return new OkObjectResult(await _monitorService.GetExceptionDetailAsync(direction, id, cancellationToken));
    }
}
