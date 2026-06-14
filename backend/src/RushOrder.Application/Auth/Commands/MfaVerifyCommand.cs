using FluentValidation;
using MediatR;
using RushOrder.Application.Auth.DTOs;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Auth.Commands;

public record MfaVerifyCommand(
    string TempToken,
    string Code,
    string? IpAddress = null) : ICommand<LoginResult>;

public sealed class MfaVerifyCommandValidator : AbstractValidator<MfaVerifyCommand>
{
    public MfaVerifyCommandValidator()
    {
        RuleFor(x => x.TempToken).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

public sealed class MfaVerifyCommandHandler : IRequestHandler<MfaVerifyCommand, LoginResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ITotpService _totpService;
    private readonly IAuthCacheService _authCacheService;
    private readonly IUnitOfWork _unitOfWork;

    public MfaVerifyCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenService jwtTokenService,
        ITotpService totpService,
        IAuthCacheService authCacheService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenService = jwtTokenService;
        _totpService = totpService;
        _authCacheService = authCacheService;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoginResult> Handle(MfaVerifyCommand request, CancellationToken cancellationToken)
    {
        var userId = await _authCacheService.GetMfaPendingUserIdAsync(request.TempToken, cancellationToken)
            ?? throw new BusinessRuleException("Invalid or expired MFA session.");

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        if (user.MfaSecret is null || !_totpService.VerifyCode(user.MfaSecret, request.Code))
            throw new BusinessRuleException("Invalid MFA code.");

        await _authCacheService.RemoveMfaPendingAsync(request.TempToken, cancellationToken);

        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var accessTokenResult = _jwtTokenService.GenerateAccessToken(user, user.TenantId);

        var refreshToken = RefreshToken.Create(
            userId: user.Id,
            tokenHash: RefreshToken.HashToken(rawRefreshToken),
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            createdByIp: request.IpAddress ?? "unknown");

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResult(
            RequiresMfa: false,
            TempToken: null,
            Tokens: new TokenPairDto(accessTokenResult.Token, rawRefreshToken, accessTokenResult.ExpiresAt),
            User: new UserInfoDto(user.Id, user.Email.Value, user.Role.ToString(), user.RestaurantIds));
    }
}
