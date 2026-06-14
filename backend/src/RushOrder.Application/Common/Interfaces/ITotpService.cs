namespace RushOrder.Application.Common.Interfaces;

public interface ITotpService
{
    string GenerateSecret();
    bool VerifyCode(string secret, string code, int windowSteps = 1);
    string BuildOtpAuthUri(string secret, string issuer, string accountName);
    string GenerateQrCodeBase64(string otpAuthUri);
}
