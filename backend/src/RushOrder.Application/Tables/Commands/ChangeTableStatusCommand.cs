using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Tables.Commands;

public record ChangeTableStatusCommand(Guid TableId, TableStatus NewStatus) : ICommand<Unit>;

public sealed class ChangeTableStatusCommandValidator : AbstractValidator<ChangeTableStatusCommand>
{
    public ChangeTableStatusCommandValidator()
    {
        RuleFor(x => x.TableId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}

public sealed class ChangeTableStatusCommandHandler : IRequestHandler<ChangeTableStatusCommand, Unit>
{
    private readonly ITableRepository _tableRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeTableStatusCommandHandler(ITableRepository tableRepository, IUnitOfWork unitOfWork)
    {
        _tableRepository = tableRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(ChangeTableStatusCommand request, CancellationToken cancellationToken)
    {
        var table = await _tableRepository.GetByIdAsync(request.TableId, cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.TableId);

        table.SetStatus(request.NewStatus);

        await _tableRepository.UpdateAsync(table, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
