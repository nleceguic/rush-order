using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Auth.Commands;

public record ForgotPasswordCommand(string Email) : ICommand<Unit>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Unit>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthCacheService _authCacheService;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepository,
        IAuthCacheService authCacheService,
        INotificationService notificationService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _authCacheService = authCacheService;
        _notificationService = notificationService;
        _configuration = configuration;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetActiveByEmailAsync(request.Email, cancellationToken);
        if (user is null) return Unit.Value; // Don't leak whether email exists

        var rawToken = await _authCacheService.StorePasswordResetAsync(user.Id, cancellationToken);
        var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var resetLink = $"{frontendUrl.TrimEnd('/')}/reset-password?token={rawToken}";

        await _notificationService.SendPasswordResetAsync(user.Email.Value, resetLink, cancellationToken);

        return Unit.Value;
    }
}
