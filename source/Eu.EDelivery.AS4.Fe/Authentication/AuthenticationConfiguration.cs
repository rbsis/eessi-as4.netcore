namespace Eu.EDelivery.AS4.Fe.Authentication;

public class AuthenticationConfiguration
{
    public required string ConnectionString { get; set; }
    public required string Provider { get; set; }
    public required Jwt JwtOptions { get; set; }
}

public class Jwt
{
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int ValidFor { get; set; }
    public required string Key { get; set; }
}
