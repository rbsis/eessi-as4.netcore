using System.Net;
using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eu.EDelivery.AS4.Fe.Controllers;

/// <summary>
/// Authentication controller
/// </summary>
/// <seealso cref="Controller" />
[Route("api/[controller]")]
public class AuthenticationController : Controller
{
    private readonly ITokenService _tokenService;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationController"/> class.
    /// </summary>
    /// <param name="tokenService">The token service.</param>
    /// <param name="userManager">The user manager.</param>
    public AuthenticationController(ITokenService tokenService, UserManager<ApplicationUser> userManager)
    {
        _tokenService = tokenService;
        _userManager = userManager;
    }

    /// <summary>
    /// Login using username / password combination
    /// </summary>
    /// <param name="login">The login payload</param>
    /// <returns>
    /// Json containing access token if login has succeeded
    /// </returns>
    [HttpPost]
    [AllowAnonymous]
    [SwaggerResponse((int)HttpStatusCode.OK, "Login was successful", typeof(LoginSuccessModel))]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized, "Login failed", typeof(UnauthorizedResult))]
    [ProducesResponseType(typeof(UnauthorizedResult), (int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginModel login)
    {
        var user = await _userManager.FindByNameAsync(login.Username);
        if (user == null)
        {
            return new UnauthorizedResult();
        }

        var result = await _userManager.CheckPasswordAsync(user, login.Password);
        if (result)
        {
            return new OkObjectResult(new LoginSuccessModel
            {
                AccessToken = await _tokenService.GenerateTokenAsync(user)
            });
        }

        return new UnauthorizedResult();
    }

    /// <summary>
    /// Login using external provider
    /// </summary>
    /// <param name="provider">The name of the provider</param>
    /// <returns></returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("externallogin")]
    [SwaggerResponse((int)HttpStatusCode.OK, "External login initiated", typeof(OkResult))]
    public async Task<IActionResult> ExternalLogin(string? provider = null)
    {
        await HttpContext.ChallengeAsync(provider, new AuthenticationProperties { RedirectUri = "http://localhost:3000/#/login?callback=true" });
        return new OkResult();
    }

    /// <summary>
    /// Callback url used for external providers after login
    /// </summary>
    /// <param name="provider">The name of the provider</param>
    /// <returns></returns>
    [HttpGet]
    [Authorize]
    [Route("externallogincallback")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Login was successful", typeof(LoginSuccessModel))]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized, "Login failed", typeof(UnauthorizedResult))]
    public async Task<IActionResult> ExternalLoginCallback(string provider)
    {
        var isAuthenticated = await HttpContext.AuthenticateAsync(provider);
        if (isAuthenticated.Principal?.Identity?.IsAuthenticated != true)
        {
            return new UnauthorizedResult();
        }

        await HttpContext.SignOutAsync("Cookies");
        var applicationUser = await _userManager.GetUserAsync(User);
        if (applicationUser == null)
        {
            return new UnauthorizedResult();
        }

        return new OkObjectResult(new LoginSuccessModel
        {
            AccessToken = await _tokenService.GenerateTokenAsync(applicationUser)
        });
    }
}
