using System.ComponentModel.DataAnnotations;

namespace RushOrder.API.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Jwt:Issuer is required")]
    public string Issuer { get; init; } = string.Empty;

    [Required(ErrorMessage = "Jwt:Audience is required")]
    public string Audience { get; init; } = string.Empty;

    public int AccessTokenExpirationMinutes { get; init; } = 15;
    public int RefreshTokenExpirationDays { get; init; } = 30;
    public string PrivateKeyPath { get; init; } = "keys/private.pem";
    public string PublicKeyPath { get; init; } = "keys/public.pem";
}
