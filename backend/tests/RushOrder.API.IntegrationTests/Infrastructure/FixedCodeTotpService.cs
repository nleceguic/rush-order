using RushOrder.Application.Common.Interfaces;

namespace RushOrder.API.IntegrationTests.Infrastructure;

/// <summary>
/// Deterministic TOTP fake for tests: always accepts <see cref="ValidCode"/> as a valid code.
/// Eliminates time-dependency from MFA integration tests.
/// </summary>
public sealed class FixedCodeTotpService : ITotpService
{
    public const string ValidCode   = "123456";
    public const string TestSecret  = "JBSWY3DPEHPK3PXP";

    public string GenerateSecret() => TestSecret;

    public bool VerifyCode(string secret, string code, int windowSteps = 1)
        => code == ValidCode;

    public string BuildOtpAuthUri(string secret, string issuer, string accountName)
        => $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}" +
           $"?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";

    public string GenerateQrCodeBase64(string otpAuthUri) => "dGVzdA==";
}
