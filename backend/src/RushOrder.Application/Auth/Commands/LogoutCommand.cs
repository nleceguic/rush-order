using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Auth.Commands;

public record LogoutCommand(
    string Jti,
    DateTimeOffset AccessTokenExpiry,
    string RawRefreshToken) : ICommand<Unit>;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.Jti).NotEmpty();
        RuleFor(x => x.RawRefreshToken).NotEmpty();
    }
}

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthCacheService _authCacheService;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IAuthCacheService authCacheService,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _authCacheService = authCacheService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshToken.HashToken(request.RawRefreshToken);
        var refreshToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (refreshToken?.IsActive == true)
            refreshToken.Revoke();

        var remaining = request.AccessTokenExpiry - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
            await _authCacheService.BlockJtiAsync(request.Jti, remaining, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
