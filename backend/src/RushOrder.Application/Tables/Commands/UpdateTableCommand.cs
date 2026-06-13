using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Tables.Commands;

public record UpdateTableCommand(
    Guid TableId,
    string? Name,
    int? Capacity,
    string? Zone,
    double? PositionX,
    double? PositionY) : ICommand<Unit>;

public sealed class UpdateTableCommandValidator : AbstractValidator<UpdateTableCommand>
{
    public UpdateTableCommandValidator()
    {
        RuleFor(x => x.TableId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 50).When(x => x.Capacity.HasValue);
        RuleFor(x => x.Zone).MaximumLength(100).When(x => x.Zone is not null);
    }
}

public sealed class UpdateTableCommandHandler : IRequestHandler<UpdateTableCommand, Unit>
{
    private readonly ITableRepository _tableRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTableCommandHandler(ITableRepository tableRepository, IUnitOfWork unitOfWork)
    {
        _tableRepository = tableRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(UpdateTableCommand request, CancellationToken cancellationToken)
    {
        var table = await _tableRepository.GetByIdAsync(request.TableId, cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.TableId);

        table.UpdateDetails(
            request.Name ?? table.Name,
            request.Capacity ?? table.Capacity,
            request.Zone ?? table.Zone);

        if (request.PositionX.HasValue && request.PositionY.HasValue)
            table.SetPosition(request.PositionX.Value, request.PositionY.Value);

        await _tableRepository.UpdateAsync(table, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
