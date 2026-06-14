using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Auth.Commands;

public record MfaConfirmCommand(Guid UserId, string Code) : ICommand<Unit>;

public sealed class MfaConfirmCommandValidator : AbstractValidator<MfaConfirmCommand>
{
    public MfaConfirmCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

public sealed class MfaConfirmCommandHandler : IRequestHandler<MfaConfirmCommand, Unit>
{
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly IUnitOfWork _unitOfWork;

    public MfaConfirmCommandHandler(IUserRepository userRepository, ITotpService totpService, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _totpService = totpService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(MfaConfirmCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        if (user.PendingMfaSecret is null)
            throw new BusinessRuleException("No MFA setup in progress.");

        if (!_totpService.VerifyCode(user.PendingMfaSecret, request.Code))
            throw new BusinessRuleException("Invalid MFA code.");

        user.ActivatePendingMfa([]);

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
