namespace RushOrder.Infrastructure.Settings;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "RushOrder";
    public string Audience { get; set; } = "RushOrder.API";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 30;
    public string PrivateKeyPath { get; set; } = "keys/private.pem";
    public string PublicKeyPath { get; set; } = "keys/public.pem";
}
