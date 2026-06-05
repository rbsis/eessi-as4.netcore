namespace Eu.EDelivery.AS4.Fe.Models;

/// <summary>
/// Class containing setup data
/// </summary>
public class Setup
{
    /// <summary>
    /// Gets or sets the admin password.
    /// </summary>
    /// <value>
    /// The admin password.
    /// </value>
    public required string AdminPassword { get; set; }
    /// <summary>
    /// Gets or sets the readonly password.
    /// </summary>
    /// <value>
    /// The readonly password.
    /// </value>
    public required string ReadonlyPassword { get; set; }
    /// <summary>
    /// Gets or sets the JWT key.
    /// </summary>
    /// <value>
    /// The JWT key.
    /// </value>
    public required string JwtKey { get; set; }
}
