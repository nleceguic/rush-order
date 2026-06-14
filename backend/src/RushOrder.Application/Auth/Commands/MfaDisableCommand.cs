using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Auth.Commands;

public record MfaDisableCommand(Guid UserId, string Code) : ICommand<Unit>;

public sealed class MfaDisableCommandValidator : AbstractValidator<MfaDisableCommand>
{
    public MfaDisableCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

public sealed class MfaDisableCommandHandler : IRequestHandler<MfaDisableCommand, Unit>
{
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly IUnitOfWork _unitOfWork;

    public MfaDisableCommandHandler(IUserRepository userRepository, ITotpService totpService, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _totpService = totpService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(MfaDisableCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        if (!user.MfaEnabled)
            throw new BusinessRuleException("MFA is not enabled on this account.");

        if (!_totpService.VerifyCode(user.MfaSecret!, request.Code))
            throw new BusinessRuleException("Invalid MFA code.");

        user.DisableMfa();

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
