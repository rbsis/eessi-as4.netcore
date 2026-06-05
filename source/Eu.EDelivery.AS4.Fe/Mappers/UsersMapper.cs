using Eu.EDelivery.AS4.Fe.Authentication;
using Eu.EDelivery.AS4.Fe.Users;

namespace Eu.EDelivery.AS4.Fe.Mappers;

/// <summary>
/// User mapper
/// </summary>
public class UsersMapper :
    IMapper<ApplicationUser, User>
{
    public User Map(ApplicationUser source) => new()
    {
        Name = source.UserName,
        Roles = source.Claims
            .Where(z => !string.IsNullOrEmpty(z.ClaimValue))
            .Select(z => z.ClaimValue!)
            .ToArray()
    };
}
