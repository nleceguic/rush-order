using MediatR;
using RushOrder.Application.Admin.DTOs;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Admin.Commands;

public sealed record ImpersonateTenantCommand(Guid TenantId) : ICommand<ImpersonateTokenDto>;

public sealed class ImpersonateTenantCommandHandler
    : IRequestHandler<ImpersonateTenantCommand, ImpersonateTokenDto>
{
    private readonly ITenantRepository _tenantRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IJwtTokenService _jwtTokenService;

    public ImpersonateTenantCommandHandler(
        ITenantRepository tenantRepo,
        ICurrentUserService currentUser,
        IJwtTokenService jwtTokenService)
    {
        _tenantRepo = tenantRepo;
        _currentUser = currentUser;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<ImpersonateTokenDto> Handle(
        ImpersonateTenantCommand request, CancellationToken cancellationToken)
    {
        _ = await _tenantRepo.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", request.TenantId);

        var adminUserId = _currentUser.UserId
            ?? throw new BusinessRuleException("Admin user context is required.");

        var tokenResult = _jwtTokenService.GenerateImpersonationToken(
            adminUserId, _currentUser.Email ?? string.Empty, request.TenantId);

        return new ImpersonateTokenDto(tokenResult.Token, tokenResult.ExpiresAt);
    }
}
