using System.Net;
using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Services;
using Eu.EDelivery.AS4.Fe.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eu.EDelivery.AS4.Fe.Controllers;

/// <summary>
///     Controller to manage users
/// </summary>
/// <seealso cref="Controller" />
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class UserController : Controller
{
    private readonly IUserService _userService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserController"/> class.
    /// </summary>
    /// <param name="userService">The user service.</param>
    public UserController(IUserService userService)
    {
        this._userService = userService;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    /// <returns>List of users</returns>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.ExpectationFailed, "Password requirements were not met or something else went wrong.", typeof(ErrorModel))]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => new OkObjectResult(await _userService.GetAsync(cancellationToken));

    /// <summary>
    /// Creates the specified new user.
    /// </summary>
    /// <param name="newUser">The new user.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.ExpectationFailed, "User already exists", typeof(ErrorModel))]
    public async Task<IActionResult> Create([FromBody] NewUser newUser, CancellationToken cancellationToken)
    {
        await _userService.CreateAsync(newUser, cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Delete an existing user.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpDelete]
    [Route("{username}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.ExpectationFailed, "User doesn't exist", typeof(ErrorModel))]
    public async Task<IActionResult> Delete(string username, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(username, cancellationToken);
        return new OkResult();
    }

    /// <summary>
    /// Change a user password
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="update">The update.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPut]
    [Route("{username}")]
    [Authorize(Roles = Roles.Admin)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.ExpectationFailed, "User doesn't exist or password requirements were not met.", typeof(ErrorModel))]
    public async Task<IActionResult> Update(string username, [FromBody] UpdateUser update, CancellationToken cancellationToken)
    {
        await _userService.UpdateAsync(username, update, cancellationToken);
        return new OkResult();
    }
}
