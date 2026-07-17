using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Recommendations.Commands;

public record DeletePairingRuleCommand(Guid Id) : ICommand<Unit>;

public sealed class DeletePairingRuleCommandValidator : AbstractValidator<DeletePairingRuleCommand>
{
    public DeletePairingRuleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class DeletePairingRuleCommandHandler : IRequestHandler<DeletePairingRuleCommand, Unit>
{
    private readonly IPairingRuleRepository _pairingRules;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePairingRuleCommandHandler(IPairingRuleRepository pairingRules, IUnitOfWork unitOfWork)
    {
        _pairingRules = pairingRules;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(DeletePairingRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _pairingRules.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductPairingRule), request.Id);

        await _pairingRules.DeleteAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
