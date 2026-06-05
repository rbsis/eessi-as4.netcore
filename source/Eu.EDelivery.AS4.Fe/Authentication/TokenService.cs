using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Eu.EDelivery.AS4.Fe.Authentication;

public class TokenService : ITokenService
{
    private readonly IOptionsSnapshot<JwtOptions> _jwtOptions;
    private readonly UserManager<ApplicationUser> _userManager;

    public TokenService(IOptionsSnapshot<JwtOptions> jwtOptions, UserManager<ApplicationUser> userManager)
    {
        _jwtOptions = jwtOptions;
        _userManager = userManager;
    }

    public async Task<string> GenerateTokenAsync(ApplicationUser user)
    {
        var options = _jwtOptions.Value;
        var claims = await _userManager.GetClaimsAsync(user);

        var jwt = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            options.NotBefore,
            options.Expiration,
            options.SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
