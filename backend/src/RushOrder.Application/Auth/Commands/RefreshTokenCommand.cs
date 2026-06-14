using FluentValidation;
using MediatR;
using RushOrder.Application.Auth.DTOs;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Auth.Commands;

public record RefreshTokenCommand(string RawRefreshToken, string? IpAddress = null) : ICommand<TokenPairDto>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RawRefreshToken).NotEmpty();
}

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenPairDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<TokenPairDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshToken.HashToken(request.RawRefreshToken);
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new BusinessRuleException("Invalid refresh token.");

        if (existingToken.IsRevoked)
        {
            // Theft detected: revoke entire family
            var family = await _refreshTokenRepository.GetActiveFamilyAsync(existingToken.FamilyId, cancellationToken);
            foreach (var t in family) t.Revoke();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new BusinessRuleException("Refresh token reuse detected. All sessions have been revoked.");
        }

        if (existingToken.IsExpired)
            throw new BusinessRuleException("Refresh token has expired.");

        var user = await _userRepository.GetByIdAsync(existingToken.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), existingToken.UserId);

        var rawNewRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var newTokenHash = RefreshToken.HashToken(rawNewRefreshToken);
        var accessTokenResult = _jwtTokenService.GenerateAccessToken(user, user.TenantId);

        existingToken.Revoke(replacedByTokenHash: newTokenHash);

        var newRefreshToken = RefreshToken.Create(
            userId: user.Id,
            tokenHash: newTokenHash,
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            createdByIp: request.IpAddress ?? "unknown",
            familyId: existingToken.FamilyId);

        await _refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TokenPairDto(accessTokenResult.Token, rawNewRefreshToken, accessTokenResult.ExpiresAt);
    }
}
