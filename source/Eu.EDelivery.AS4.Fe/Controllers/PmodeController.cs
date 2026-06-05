using System.Net;
using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Pmodes.Model;
using Eu.EDelivery.AS4.Fe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eu.EDelivery.AS4.Fe.Controllers;

/// <summary>
/// Controller to manage pmodes
/// </summary>
/// <seealso cref="Controller" />
[Route("api/[controller]")]
public class PmodeController : Controller
{
    private readonly IPmodeService _pmodeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PmodeController"/> class.
    /// </summary>
    /// <param name="pmodeService">The pmode service.</param>
    public PmodeController(IPmodeService pmodeService)
    {
        this._pmodeService = pmodeService;
    }

    /// <summary>
    /// Get a list of receiving pmode names.
    /// </summary>
    /// <returns>String list with all the pmode names.</returns>
    [HttpGet]
    [Route("receiving")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Receiving pmodes retrieved successfully", typeof(OkResult))]
    public async Task<IEnumerable<string>> GetReceivingPmodes(CancellationToken cancellationToken) =>
        await _pmodeService.GetReceivingNamesAsync(cancellationToken);

    /// <summary>
    /// Create a receiving pmode
    /// </summary>
    /// <param name="basePmode">Pmode data</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("receiving")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Receiving pmode created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task CreateReceiving([FromBody] ReceivingBasePmode basePmode, CancellationToken cancellationToken) =>
        await _pmodeService.CreateReceivingAsync(basePmode, cancellationToken);

    /// <summary>
    /// Update existing receiving pmode
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("receiving/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Receiving pmode updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task UpdateReceiving([FromBody] ReceivingBasePmode basePmode, string originalName, CancellationToken cancellationToken) =>
        await _pmodeService.UpdateReceivingAsync(basePmode, originalName, cancellationToken);

    /// <summary>
    /// Get a receiving pmode by name
    /// </summary>
    /// <param name="name">The name of the receiving pmode</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("receiving/{name}")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Receiving pmode retrieved successfully", typeof(ReceivingBasePmode))]
    public async Task<ReceivingBasePmode?> GetReceiving(string name, CancellationToken cancellationToken) =>
        await _pmodeService.GetReceivingByNameAsync(name, cancellationToken);

    /// <summary>
    /// Delete an existing receiving pmode.
    /// </summary>
    /// <param name="name">The name of the pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("receiving/{name}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Receiving pmode deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested receiving pmode doesn't exist", typeof(ErrorModel))]
    public async Task DeleteReceiving(string name, CancellationToken cancellationToken) =>
        await _pmodeService.DeleteReceivingAsync(name, cancellationToken);

    /// <summary>
    /// Get a list of sending pmode names
    /// </summary>
    /// <returns>String list of names</returns>
    [HttpGet]
    [Route("sending")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Sending pmodes retrieved successfully", typeof(OkResult))]
    public async Task<IEnumerable<string>> GetSendingPmodes(CancellationToken cancellationToken) =>
        await _pmodeService.GetSendingNamesAsync(cancellationToken);

    /// <summary>
    /// Create a sending pmode.
    /// </summary>
    /// <param name="basePmode">The pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("sending")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Sending pmode created successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task CreateSending([FromBody] SendingBasePmode basePmode, CancellationToken cancellationToken) =>
        await _pmodeService.CreateSendingAsync(basePmode, cancellationToken);

    /// <summary>
    /// Get a sending pmode by name.
    /// </summary>
    /// <param name="name">The name of the pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("sending/{name}")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Sending pmode retrieved successfully", typeof(SendingBasePmode))]
    public async Task<SendingBasePmode?> GetSending(string name, CancellationToken cancellationToken) =>
        await _pmodeService.GetSendingByNameAsync(name, cancellationToken);

    /// <summary>
    /// Delete an existing sending pmode.
    /// </summary>
    /// <param name="name">The name of the pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("sending/{name}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Sending pmode deleted successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Returned when the requested sending pmode doesn't exist", typeof(ErrorModel))]
    public async Task DeleteSending(string name, CancellationToken cancellationToken) =>
        await _pmodeService.DeleteSendingAsync(name, cancellationToken);

    /// <summary>
    /// Update an existing pmode.
    /// </summary>
    /// <param name="basePmode">The pmode data.</param>
    /// <param name="originalName">Name of the original pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("sending/{originalName}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Sending pmode updated successfully", typeof(OkResult))]
    [SwaggerResponse((int)HttpStatusCode.Conflict, "Indicates that another entity already exists", typeof(ErrorModel))]
    public async Task UpdateSending([FromBody] SendingBasePmode basePmode, string originalName, CancellationToken cancellationToken) =>
        await _pmodeService.UpdateSendingAsync(basePmode, originalName, cancellationToken);
}
