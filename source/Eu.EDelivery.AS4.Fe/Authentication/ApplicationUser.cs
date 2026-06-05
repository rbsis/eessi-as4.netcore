using Microsoft.AspNetCore.Identity;

namespace Eu.EDelivery.AS4.Fe.Authentication;

public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Navigation property for the claims this user possesses.
    /// </summary>
    public virtual ICollection<IdentityUserClaim<string>> Claims { get; } = [];
}
