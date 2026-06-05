using System.Net;
using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Fe.SmpConfiguration.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eu.EDelivery.AS4.Fe.Controllers;

/// <summary>
///     Smp configuration controller
/// </summary>
[Route("api/[controller]")]
public class SmpConfigurationController
{
    private readonly ISmpConfigurationService _smpConfiguration;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SmpConfigurationController" /> class.
    /// </summary>
    /// <param name="smpConfiguration">The SMP configuration.</param>
    public SmpConfigurationController(ISmpConfigurationService smpConfiguration)
    {
        _smpConfiguration = smpConfiguration;
    }

    /// <summary>
    ///     Get all Smp configurations
    /// </summary>
    /// <returns>List of SMP configurations</returns>
    [HttpGet]
    [SwaggerResponse((int)HttpStatusCode.OK, "SMP configurations retrieved successfully", typeof(IEnumerable<SmpConfigurationRecord>))]
    public async Task<IEnumerable<SmpConfigurationRecord>> Get(CancellationToken cancellationToken) => await _smpConfiguration.GetRecordsAsync(cancellationToken);

    /// <summary>
    ///     Gets Smp configuration by identifier
    /// </summary>
    /// <param name="id">The identifier</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Matching Smp configuration</returns>
    [HttpGet]
    [Route("{id}")]
    [SwaggerResponse((int)HttpStatusCode.OK, "SMP configuration retrieved successfully", typeof(SmpConfigurationDetail))]
    public async Task<SmpConfigurationDetail> Get(int id, CancellationToken cancellationToken) => await _smpConfiguration.GetByIdAsync(id, cancellationToken);

    /// <summary>
    ///     Posts the specified SMP configuration.
    /// </summary>
    /// <param name="smpConfiguration">The SMP configuration.</param>
    /// <param name="cancellationToken"></param>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK, "SMP configuration created successfully", typeof(SmpConfigurationDetail))]
    public async Task<IActionResult> Post([FromBody] SmpConfigurationDetail smpConfiguration, CancellationToken cancellationToken)
    {
        var configuration = await _smpConfiguration.CreateAsync(smpConfiguration, cancellationToken);
        return new OkObjectResult(configuration);
    }

    /// <summary>
    ///     Puts the specified identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="smpConfiguration">The SMP configuration.</param>
    /// <param name="cancellationToken"></param>
    [HttpPut]
    [Route("{id}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    public async Task<IActionResult> Put(int id, [FromBody] SmpConfigurationDetail smpConfiguration, CancellationToken cancellationToken)
    {
        await _smpConfiguration.UpdateAsync(id, smpConfiguration, cancellationToken);
        return new OkResult();
    }

    /// <summary>
    ///     Delete an existing <see cref="Entities.SmpConfiguration" />
    /// </summary>
    /// <param name="id">The id of the <see cref="Entities.SmpConfiguration" /></param>
    /// <param name="cancellationToken"></param>
    [HttpDelete]
    [Route("{id}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _smpConfiguration.DeleteAsync(id, cancellationToken);
        return new OkResult();
    }
}
