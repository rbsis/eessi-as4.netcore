using System.Net;
using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.SubmitTool;
using HttpMultipartParser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eu.EDelivery.AS4.Fe.Controllers;

/// <summary>
///     Controller for the submit tool
/// </summary>
/// <seealso cref="Controller" />
[Route("api/[controller]")]
public class SubmitToolController : Controller
{
    private readonly ISubmitMessageCreator _submitMessageCreator;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SubmitToolController" /> class.
    /// </summary>
    /// <param name="submitMessageCreator">The submit message creator.</param>
    public SubmitToolController(ISubmitMessageCreator submitMessageCreator)
    {
        this._submitMessageCreator = submitMessageCreator;
    }

    /// <summary>
    ///     Post method to submit a message
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        var parser = MultipartFormDataParser.Parse(Request.Body);
        _ = int.TryParse(parser.GetParameterValue("messages"), out var messages);

        var sendingPmode = parser.GetParameterValue("pmode");
        if (sendingPmode == null) throw new ArgumentNullException(nameof(sendingPmode), @"SendingPmode parameter is required!");

        await _submitMessageCreator.CreateSubmitMessagesAsync(new MessagePayload
        {
            Files = [.. parser.Files],
            SendingPmode = sendingPmode,
            NumberOfSubmitMessages = messages == 0 ? 1 : messages
        }, cancellationToken);

        return Ok();
    }
}
