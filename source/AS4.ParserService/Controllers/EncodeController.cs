//using System.Net;
using AS4.ParserService.Infrastructure;
using AS4.ParserService.Models;
using AS4.ParserService.Services;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.AspNetCore.Mvc;
//using Swashbuckle.AspNetCore.Annotations;

namespace AS4.ParserService.Controllers;

/// <summary>
/// Provides functionality to create an AS4 Message
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EncodeController : ControllerBase
{
    private readonly EncodeService _encodeService;
    private readonly ISerializerProvider _serializerProvider;

    public EncodeController(EncodeService encodeService, ISerializerProvider serializerProvider)
    {
        _encodeService = encodeService;
        _serializerProvider = serializerProvider;
    }
    /// <summary>
    /// Verify if the Encode service is up.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<string> Get()
    {
        return Ok("AS4.NET Encode");
    }

    /// <summary>
    /// Encode the given payloads into an AS4 Message using the specified Processing Mode.
    /// </summary>       
    /// <param name="encodeInformation">An <see cref="EncodeMessageInfo"/> that contains all information that is required to create an AS4 Message.</param>
    /// <returns></returns>
    [HttpPost]
    //[SwaggerResponse((int)HttpStatusCode.OK, description: "When the Encode process succeeded, an EncodeResult that contains the AS4 Message is returned", type: typeof(EncodeResult))]
    //[SwaggerResponse((int)HttpStatusCode.BadRequest, description: "When the given EncodeMessageInfo object does not contain a Sending PMode, a Bad Request is returned.")]
    //[SwaggerResponse((int)HttpStatusCode.InternalServerError, description: "Something went wrong while creating the requested AS4 Message", type: typeof(Exception))]
    public async Task<ActionResult<EncodeResult>> Post([FromBody] EncodeMessageInfo encodeInformation)
    {
        if (encodeInformation == null)
        {
            return BadRequest();
        }

        if (encodeInformation.SendingPMode == null)
        {
            return BadRequest();
        }

        var certificateInformation = CertificateInfoRetriever.RetrieveCertificatePassword(Request);
        if (certificateInformation != null)
        {
            encodeInformation.SigningCertificatePassword = certificateInformation.SigningPassword;
        }

        var result = await _encodeService.CreateAS4MessageAsync(encodeInformation, HttpContext.RequestAborted);
        if (result == null)
        {
            return BadRequest();
        }

        if (result.Exception != null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, result.Exception);
        }

        return Ok(CreateEncodeResultFromContext(result));
    }

    private EncodeResult CreateEncodeResultFromContext(MessagingContext context)
    {
        using var stream = new MemoryStream();
        var serializer = _serializerProvider.Get(context.AS4Message!.ContentType);

        serializer.Serialize(context.AS4Message, stream);

        var result = new EncodeResult
        {
            SendToUrl = context.SendingPMode?.PushConfiguration?.Protocol?.Url ?? string.Empty,
            AS4Message = stream.ToArray(),
            ContentType = context.AS4Message.ContentType.Replace("\"utf-8\"", "utf-8"),
            EbmsMessageId = context.AS4Message.GetPrimaryMessageId() ?? string.Empty
        };

        return result;
    }



}
