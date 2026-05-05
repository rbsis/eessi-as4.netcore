using System.Net;
using AS4.ParserService.Infrastructure;
using AS4.ParserService.Models;
using AS4.ParserService.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AS4.ParserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DecodeController : ControllerBase
{
    private readonly DecodeService _decodeService;

    public DecodeController(DecodeService decodeService)
    {
        _decodeService = decodeService;
    }

    /// <summary>
    /// Verify if the Decode service is up.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<string> Get()
    {
        return Ok("AS4.NET Decode");
    }

    /// <summary>
    /// Processes a received AS4 Message using the specified Receiving PMode.
    /// </summary>
    /// <param name="decodeInfo">An <see cref="DecodeMessageInfo"/> instance that contains all information that is required to decode 
    /// the received AS4 Message</param>
    /// <returns></returns>
    [HttpPost]
    [SwaggerResponse((int)HttpStatusCode.OK, description: "When the Decode process succeeded, a DecodeResult that contains the Deliver information, payloads and the responding signalmessage is returned.", type: typeof(DecodeResult))]
    [SwaggerResponse((int)HttpStatusCode.Accepted, description: "The message has been accepted")]
    [SwaggerResponse((int)HttpStatusCode.BadRequest, description: "When the given DecodeMessageInfo object does not contain a Receiving PMode or a Responding PMode, a Bad Request is returned")]
    [SwaggerResponse((int)HttpStatusCode.InternalServerError, description: "Something went wrong while creating the requested AS4 Message", type: typeof(Exception))]
    public async Task<ActionResult> Post([FromBody] DecodeMessageInfo decodeInfo)
    {
        if (decodeInfo == null)
        {
            return BadRequest();
        }

        var certificateInformation = CertificateInfoRetriever.RetrieveCertificatePassword(Request);
        if (certificateInformation != null)
        {
            decodeInfo.DecryptionCertificatePassword = certificateInformation.DecryptionPassword;
            decodeInfo.SigningResponseCertificatePassword = certificateInformation.SigningPassword;
        }

        try
        {
            var processingResult = await _decodeService.ProcessAsync(decodeInfo, HttpContext.RequestAborted);

            if (processingResult == null || processingResult.ReceivedMessageType == EbmsMessageType.Unknown)
            {
                return BadRequest();
            }

            return Ok(processingResult);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }
}
