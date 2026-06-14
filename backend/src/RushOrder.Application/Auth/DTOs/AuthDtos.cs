namespace RushOrder.Application.Auth.DTOs;

public record UserInfoDto(
    Guid Id,
    string Email,
    string Role,
    IReadOnlyList<Guid> Restaurants);

public record TokenPairDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiry);

public record LoginResult(
    bool RequiresMfa,
    string? TempToken,
    TokenPairDto? Tokens,
    UserInfoDto? User);

public record MfaSetupResult(
    string QrCodeImageBase64,
    string Secret,
    IReadOnlyList<string> BackupCodes);
