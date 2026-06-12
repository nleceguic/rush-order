using System.ComponentModel.DataAnnotations;

namespace RushOrder.API.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Jwt:Secret is required")]
    [MinLength(32, ErrorMessage = "Jwt:Secret must be at least 32 characters")]
    public string Secret { get; init; } = string.Empty;

    [Required(ErrorMessage = "Jwt:Issuer is required")]
    public string Issuer { get; init; } = string.Empty;

    [Required(ErrorMessage = "Jwt:Audience is required")]
    public string Audience { get; init; } = string.Empty;

    public int ExpiryMinutes { get; init; } = 60;
}
