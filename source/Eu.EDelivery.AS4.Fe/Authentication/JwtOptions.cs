using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Eu.EDelivery.AS4.Fe.Authentication;

public class JwtOptions
{
    public string Issuer { get; set; } = "ExampleIssuer";
    public string Audience { get; set; } = "http://localhost:3000";
    public DateTime NotBefore { get; } = DateTime.UtcNow.AddDays(-1);
    public DateTime Expiration => IssuedAt.AddDays(ValidFor);
    public int ValidFor { get; set; } = 10;
    public string Key { get; set; } = "fdsqlkfjdsmqlkfjdlkarjezoiparuezop78979";
    public SymmetricSecurityKey SigningKey => new(Encoding.ASCII.GetBytes(Key));
    public SigningCredentials SigningCredentials => new(SigningKey, SecurityAlgorithms.HmacSha256);
    private static DateTime IssuedAt => DateTime.UtcNow;
}
