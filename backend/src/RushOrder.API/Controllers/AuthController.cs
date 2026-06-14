using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RushOrder.Application.Auth.Commands;

namespace RushOrder.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(
            request.Email,
            request.Password,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString()), ct);

        if (result.RequiresMfa)
            return Ok(new { requiresMfa = true, tempToken = result.TempToken });

        return Ok(new
        {
            requiresMfa = false,
            accessToken = result.Tokens!.AccessToken,
            refreshToken = result.Tokens.RefreshToken,
            expiresAt = result.Tokens.AccessTokenExpiry,
            user = result.User
        });
    }

    [HttpPost("mfa/verify")]
    public async Task<IActionResult> MfaVerify([FromBody] MfaVerifyRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new MfaVerifyCommand(
            request.TempToken,
            request.Code,
            HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new
        {
            accessToken = result.Tokens!.AccessToken,
            refreshToken = result.Tokens.RefreshToken,
            expiresAt = result.Tokens.AccessTokenExpiry,
            user = result.User
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(
            request.RefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

        return Ok(new
        {
            accessToken = result.AccessToken,
            refreshToken = result.RefreshToken,
            expiresAt = result.AccessTokenExpiry
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti)!;
        var expClaim = User.FindFirstValue(JwtRegisteredClaimNames.Exp)!;
        var expiry = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim));

        await _mediator.Send(new LogoutCommand(jti, expiry, request.RefreshToken), ct);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ForgotPasswordCommand(request.Email), ct);
        return Ok(new { message = "If that email is registered, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ResetPasswordCommand(request.Token, request.NewPassword), ct);
        return Ok(new { message = "Password reset successfully." });
    }

    [HttpPost("mfa/setup")]
    [Authorize]
    public async Task<IActionResult> MfaSetup(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new MfaSetupCommand(userId), ct);
        return Ok(new
        {
            qrCodeImageBase64 = result.QrCodeImageBase64,
            secret = result.Secret,
            backupCodes = result.BackupCodes
        });
    }

    [HttpPost("mfa/confirm")]
    [Authorize]
    public async Task<IActionResult> MfaConfirm([FromBody] MfaCodeRequest request, CancellationToken ct)
    {
        await _mediator.Send(new MfaConfirmCommand(GetCurrentUserId(), request.Code), ct);
        return Ok(new { message = "MFA enabled successfully." });
    }

    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<IActionResult> MfaDisable([FromBody] MfaCodeRequest request, CancellationToken ct)
    {
        await _mediator.Send(new MfaDisableCommand(GetCurrentUserId(), request.Code), ct);
        return Ok(new { message = "MFA disabled." });
    }

    private Guid GetCurrentUserId() =>
        Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID not found in token."));

    // ── Request DTOs ──────────────────────────────────────────────────────────

    public sealed record LoginRequest(string Email, string Password);
    public sealed record MfaVerifyRequest(string TempToken, string Code);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record LogoutRequest(string RefreshToken);
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(string Token, string NewPassword);
    public sealed record MfaCodeRequest(string Code);
}
