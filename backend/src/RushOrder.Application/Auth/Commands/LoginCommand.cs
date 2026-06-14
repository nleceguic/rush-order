using FluentValidation;
using MediatR;
using RushOrder.Application.Auth.DTOs;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Auth.Commands;

public record LoginCommand(
    string Email,
    string Password,
    string? IpAddress = null,
    string? UserAgent = null) : ICommand<LoginResult>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuthCacheService _authCacheService;
    private readonly IUnitOfWork _unitOfWork;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuthCacheService authCacheService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _authCacheService = authCacheService;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetActiveByEmailAsync(request.Email, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new BusinessRuleException("Invalid email or password.");

        user.RecordLogin();

        if (user.MfaEnabled)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var tempToken = await _authCacheService.StoreMfaPendingAsync(user.Id, cancellationToken);
            return new LoginResult(RequiresMfa: true, TempToken: tempToken, Tokens: null, User: null);
        }

        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var tokenHash = RefreshToken.HashToken(rawRefreshToken);
        var accessTokenResult = _jwtTokenService.GenerateAccessToken(user, user.TenantId);

        var refreshToken = RefreshToken.Create(
            userId: user.Id,
            tokenHash: tokenHash,
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
